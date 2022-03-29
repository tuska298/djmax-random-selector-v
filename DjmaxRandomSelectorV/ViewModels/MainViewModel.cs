﻿//#define debug

using Caliburn.Micro;
using DjmaxRandomSelectorV.Models;
using static DjmaxRandomSelectorV.Models.Selector;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Effects;
using System.IO;

namespace DjmaxRandomSelectorV.ViewModels
{
    public class MainViewModel : Conductor<object>
    {
        private const int SELECTOR_VERSION = 110;

        private const string RELEASE_URL = "https://github.com/wowvv0w/djmax-random-selector-v/releases";

        private int _lastSelectorVer;
        private int _lastAllTrackVer;

        private DockPanel _dockPanel;

        public FilterViewModel FilterViewModel { get; set; }
        public HistoryViewModel HistoryViewModel { get; set; }

        public AddonViewModel AddonViewModel { get; set; }
        public AddonViewModel AddonButton { get; set; }

        private readonly Setting _setting;

        public MainViewModel()
        {
            _setting = new Setting();
            _setting.Import();

            try
            {
                CheckUpdate();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message,
                    "Selector Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                _lastSelectorVer = SELECTOR_VERSION;
            }

            try
            {
                Manager.ReadAllTrackList();
                Manager.UpdateTrackList(_setting.OwnedDlcs);
            }
            catch (FileNotFoundException)
            {
                MessageBox.Show("Cannot Find AllTrackList.csv\nCreate new file.",
                    "Selector Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                Manager.UpdateAllTrackList();
            }

            FilterViewModel = new FilterViewModel();
            HistoryViewModel = new HistoryViewModel();

            AddonViewModel = new AddonViewModel(_setting);
            AddonButton = new AddonViewModel(_setting);

            UpdateAddon(_setting.Mode);
            UpdateAddon(_setting.Aider);
            UpdateAddon(_setting.Level);
        }


        #region On Start Up
        public void ShowEvent(object view)
        {
            var window = view as Window;
            AddHotKey(window);
            SetPosition(window);
        }
        private void AddHotKey(Window window)
        {
            HwndSource source;
            IntPtr handle = new WindowInteropHelper(window).Handle;
            source = HwndSource.FromHwnd(handle);
            source.AddHook(HwndHook);
            RegisterHotKey(handle, HOTKEY_ID, 0x0000, KEY_F7);
        }
        private void SetPosition(Window window)
        {
            if (_setting.Position.Length == 2)
            {
                window.Top = _setting.Position[0];
                window.Left = _setting.Position[1];
            }
            else
            {
                _setting.Position = new double[2] { window.Top, window.Left };
            }
        }
        public void GetDockPanel(object source)
        {
            var dockPanel = source as DockPanel;
            _dockPanel = dockPanel;
        }
        #endregion

        private void CheckUpdate()
        {
            (_lastSelectorVer, _lastAllTrackVer) = Manager.GetLastVersions();

            if (SELECTOR_VERSION < _lastSelectorVer)
            {
                OpenReleasePageVisibility = Visibility.Visible;
            }

            if (_setting.AllTrackVersion != _lastAllTrackVer)
            {
                Manager.UpdateAllTrackList();
                _setting.AllTrackVersion = _lastAllTrackVer;
                _setting.Export();
            }
        }

        #region Start Selector
        private void Start()
        {
            CanStart = false;
            Filter filter = FilterViewModel.Filter;

            Mode mode = _setting.Mode;
            Aider aider = _setting.Aider;
            Level level = _setting.Level;

            var favorite = filter.IncludesFavorite ? _setting.Favorite : new List<string>();
            List<string> recents = filter.Recents;

            if (IsFilterChanged)
            {
                SiftOut(filter, favorite, mode, level);
                recents.Clear();
                IsFilterChanged = false;
            }
            else
            {
                recents = CheckRecents(recents, _setting.RecentsCount);
            }

            Music selectedMusic;
            try
            {
                selectedMusic = Pick(recents);
            }
            catch (ArgumentOutOfRangeException)
            {
                MessageBox.Show("There is no music in filtered list.",
                    "Filter Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            if (aider != Aider.Observe)
            {
                InputCommand inputCommand = Find(selectedMusic);
                inputCommand.Delay = _setting.InputDelay;
                inputCommand.Starts = mode == Mode.Freestyle && aider == Aider.AutoStart;
                Select(inputCommand);
            }

            var historyItem = new HistoryItem(selectedMusic);
            HistoryViewModel.UpdateHistory(historyItem);
            
            recents.Add(selectedMusic.Title);

            CanStart = true;
        }
        #endregion

        #region On Exit
        public void CloseEvent(object view)
        {
            var window = view as Window;

            if (!_setting.SavesRecents)
            {
                FilterViewModel.Filter.Recents.Clear();
            }
            FilterViewModel.Filter.Export();

            _setting.Position = new double[2] { (window.Top < 0 ? 0 : window.Top), (window.Left < 0 ? 0 : window.Left) };
            _setting.Export();
        }
        #endregion


        #region Window Top Bar
        private Visibility _openReleasePageVisibility = Visibility.Hidden;
        public Visibility OpenReleasePageVisibility
        {
            get { return _openReleasePageVisibility; }
            set
            {
                _openReleasePageVisibility = value;
                NotifyOfPropertyChange(() => OpenReleasePageVisibility);
            }
        }

        public void OpenReleasePage()
        {
            System.Diagnostics.Process.Start(RELEASE_URL);
        }
        public void MoveWindow(object view)
        {
            var window = view as Window;
            window.DragMove();
        }
        public void MinimizeWindow(object view)
        {
            var window = view as Window;
            window.WindowState = WindowState.Minimized;
        }
        public void CloseWindow(object view)
        {
            var window = view as Window;
            window.Close();
        }
        #endregion

        #region Tab Bar
        private string _currentTab = "FILTER";
        private bool _isFilterTabSelected = true;
        private bool _isHistoryTabSelected = false;
        public string CurrentTab
        {
            get { return _currentTab; }
            set 
            { 
                _currentTab = value; 
                NotifyOfPropertyChange(() => CurrentTab);
            }
        }
        public bool IsFilterTabSelected
        {
            get { return _isFilterTabSelected; }
            set 
            {
                _isFilterTabSelected = value;
                NotifyOfPropertyChange(() => IsFilterTabSelected);
            }
        }
        public bool IsHistoryTabSelected
        {
            get { return _isHistoryTabSelected; }
            set
            {
                _isHistoryTabSelected = value;
                NotifyOfPropertyChange(() => IsHistoryTabSelected);
            }
        }
        public void LoadFilterTab()
        {
            IsFilterTabSelected = true;
            CurrentTab = "FILTER";
        }
        public void LoadHistoryTab()
        {
            IsHistoryTabSelected = true;
            CurrentTab = "HISTORY";
        }
        #endregion

        #region Another Windows
        private readonly IWindowManager _windowManager = new WindowManager();
        public void ShowInfo()
        {
            SetBlurEffect(true);
            var infoViewModel
                = new InfoViewModel(SELECTOR_VERSION, _lastSelectorVer, _setting.AllTrackVersion, SetBlurEffect);
            _windowManager.ShowDialogAsync(infoViewModel);
        }
        public void ShowSetting()
        {
            SetBlurEffect(true);
            _windowManager.ShowDialogAsync(new SettingViewModel(_setting, SetBlurEffect));
        }
        public void ShowInventory()
        {
            SetBlurEffect(true);
            _windowManager.ShowDialogAsync(new InventoryViewModel(_setting, SetBlurEffect, FilterViewModel.ReloadFilter));
        }
        #endregion


        #region Equipment
        #region Show/Hide
        public void ShowEquipment() => SetBlurEffect(true);
        public void HideEquipment() => SetBlurEffect(false);
        private void SetBlurEffect(bool turnsOn)
        {
            _dockPanel.Effect = turnsOn ? new BlurEffect() { Radius = 75 } : null;
        }
        #endregion
        #region Constants
        private const string OFF = "OFF";
        private const string FREESTYLE = "FREESTYLE";
        private const string ONLINE = "ONLINE";
        private const string AUTO_START = "AUTO START";
        private const string OBSERVE = "OBSERVE";
        private const string BEGINNER = "BEGINNER";
        private const string MASTER = "MASTER";
        #endregion        
        #region Except
        public int RecentsCount
        {
            get => _setting.RecentsCount;
            set
            {
                _setting.RecentsCount = value;
                NotifyOfPropertyChange(() => RecentsCount);
                AddonViewModel.ExceptCount = value;
                AddonButton.ExceptCount = value;
            }
        }
        #endregion
        #region Mode
        private string _modeText;
        public string ModeText
        {
            get => _modeText;
            set
            {
                _modeText = value;
                NotifyOfPropertyChange(() => ModeText);
            }
        }

        private void UpdateAddon(Mode mode)
        {
            switch (mode)
            {
                case Mode.Freestyle: ModeText = FREESTYLE; break;
                case Mode.Online: ModeText = ONLINE; break;
            }
            AddonViewModel.SetBitmapImage(mode);
            AddonButton.SetBitmapImage(mode);
            IsFilterChanged = true;
        }
        public void SwitchMode()
        {
            if (_setting.Mode == Mode.Freestyle)
            {
                _setting.Mode = Mode.Online;
            }
            else
            {
                _setting.Mode = Mode.Freestyle;
            }
            UpdateAddon(_setting.Mode);
        }
        #endregion
        #region Aider
        private string _aiderText;
        public string AiderText
        {
            get => _aiderText;
            set
            {
                _aiderText = value;
                NotifyOfPropertyChange(() => AiderText);
            }
        }
        
        private void UpdateAddon(Aider aider)
        {
            switch (aider)
            {
                case Aider.Off: AiderText = OFF; break;
                case Aider.AutoStart: AiderText = AUTO_START; break;
                case Aider.Observe: AiderText = OBSERVE; break;
            }
            AddonViewModel.SetBitmapImage(aider);
            AddonButton.SetBitmapImage(aider);
        }

        public void PrevAider()
        {
            if (_setting.Aider == Aider.Off)
            {
                _setting.Aider = Aider.Observe;
            }
            else
            {
                _setting.Aider--;
            }
            UpdateAddon(_setting.Aider);
        }
        public void NextAider()
        {
            if (_setting.Aider == Aider.Observe)
            {
                _setting.Aider = Aider.Off;
            }
            else
            {
                _setting.Aider++;
            }
            UpdateAddon(_setting.Aider);
        }
        #endregion
        #region Level
        private string _levelText;
        public string LevelText
        {
            get => _levelText;
            set
            {
                _levelText = value;
                NotifyOfPropertyChange(() => LevelText);
            }
        }
        
        private void UpdateAddon(Level level)
        {
            switch (level)
            {
                case Level.Off: LevelText = OFF; break;
                case Level.Beginner: LevelText = BEGINNER; break;
                case Level.Master: LevelText = MASTER; break;
            }
            AddonViewModel.SetBitmapImage(level);
            AddonButton.SetBitmapImage(level);
            IsFilterChanged = true;
        }

        public void PrevLevel()
        {
            if (_setting.Level == Level.Off)
            {
                _setting.Level = Level.Master;
            }
            else
            {
                _setting.Level--;
            }
            UpdateAddon(_setting.Level);
        }
        public void NextLevel()
        {
            if (_setting.Level == Level.Master)
            {
                _setting.Level = Level.Off;
            }
            else
            {
                _setting.Level++;
            }
            UpdateAddon(_setting.Level);
        }
        #endregion
        #endregion


        #region Other Constants & Methods
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 9000;
        private const uint KEY_F7 = 118;
        private const string DJMAX_TITLE = "DJMAX RESPECT V";

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                int vkey = ((int)lParam >> 16) & 0xFFFF;
                if (vkey == KEY_F7)
                {
                    string windowTitle = GetActiveWindowTitle();
#if debug
                    Thread thread = new Thread(new ThreadStart(() => Start()));
                    thread.Start();
#else
                    if (CanStart && windowTitle == DJMAX_TITLE || _setting.Aider == Aider.Observe)
                    {
                        Thread thread = new Thread(new ThreadStart(() => Start()));
                        thread.Start();
                    }
                    else if (windowTitle != DJMAX_TITLE)
                    {
                        MessageBox.Show("Foreground window is not DJMAX RESPECT V.",
                            "Selector Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
#endif
                }

                handled = true;
            }
            return IntPtr.Zero;
        }
        private string GetActiveWindowTitle()
        {
            const int nChars = 256;
            StringBuilder Buff = new StringBuilder(nChars);
            IntPtr handle = GetForegroundWindow();

            if (GetWindowText(handle, Buff, nChars) > 0)
            {
                return Buff.ToString();
            }
            return null;
        }
#endregion
    }
}
