﻿using Dmrsv.Data.DataTypes;
using Dmrsv.Data.Enums;
using Dmrsv.Data.Context.Schema;
using Dmrsv.Data.Interfaces;

namespace Dmrsv.RandomSelector.Sifters
{
    public class PlaylistSifter : ISifter
    {
        private delegate List<Music> SiftingMethod(List<Track> tracks, List<PlaylistItem> playlist);
        private SiftingMethod? _method;
        public string CurrentMethod
        {
            get { return _method!.Method.Name; }
        }

        public void ChangeMethod(FilterOption filterOption)
        {
            _method = filterOption.Mode switch
            {
                Mode.Freestyle => SiftAll,
                Mode.Online => SiftAllAsFree,
                _ => throw new NotSupportedException(),
            };
        }

        public void SetMethod(string methodName)
        {
            _method = (SiftingMethod)Delegate.CreateDelegate(typeof(SiftingMethod), this, methodName);
        }

        public List<Music> Sift(List<Track> tracks, IFilter filterToConvert)
        {
            var filter = (SelectiveFilter)filterToConvert;
            return _method!.Invoke(tracks, filter.Playlist);
        }

        private List<Music> SiftAll(List<Track> tracks, List<PlaylistItem> playlist)
        {
            Track? track;
            var musics = new List<Music>();
            foreach (var item in playlist)
            {
                track = tracks.Find(t => t.Title.Equals(item.Title));
                if (track != null)
                {
                    musics.Add(new Music
                    {
                        Title = track.Title,
                        Style = item.Style,
                        Level = track.Patterns[item.Style].ToString()
                    });
                }
            }
            return musics;
        }

        private List<Music> SiftAllAsFree(List<Track> tracks, List<PlaylistItem> playlist)
        {
            Track? track;
            var musics = new List<Music>();
            foreach (var item in playlist)
            {
                track = tracks.Find(t => t.Title.Equals(item.Title));
                if (track != null)
                {
                    musics.Add(new Music
                    {
                        Title = track.Title,
                        Style = "FREE",
                        Level = "-"
                    });
                }
            }
            return musics;
        }
    }
}