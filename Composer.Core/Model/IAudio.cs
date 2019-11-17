﻿using System;


namespace Composer.Core.Model
{
    public interface IAudio
    {
        event EventHandler Ready;
        event EventHandler<Song> Playing;
        event EventHandler<Track> Recording;
        event EventHandler Stopped;

        int SamplesPerSecond { get; }

        void Record(Track track);

        void Play(Song song, int position = 0);

        void Stop();
    }
}
