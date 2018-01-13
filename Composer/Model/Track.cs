﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Media.MediaProperties;

namespace Composer.Model
{
    public class Track
    {
        private const int ElementSize = sizeof(float);

        public AudioFrameInputNode FrameInputNode { get; set; }
        public AudioFrameOutputNode FrameOutputNode { get; set; }

        public Status Status { get; set; } = Status.Stopped;

        public string Name { get; set; }
        public ConcurrentList<Bar> Bars { get; set; } = new ConcurrentList<Bar>();
        public int SamplesPerBar { get; set; }
        public bool IsMuted { get; set; }

        public event EventHandler StatusChanged;

        public event EventHandler<Model.Bar> BarAdded;

        public Track(string name, Audio audio)
        {
            var encodingProperties = audio.Graph.EncodingProperties;
            encodingProperties.ChannelCount = 1;

            Name = name;
            FrameInputNode = audio.Graph.CreateFrameInputNode(encodingProperties);
            FrameOutputNode = audio.Graph.CreateFrameOutputNode(encodingProperties);

            audio.DeviceInputNode.AddOutgoingConnection(FrameOutputNode);
            FrameInputNode.AddOutgoingConnection(audio.DeviceOutputNode);
        }

        public unsafe void Write(int position)
        {
            if (Status == Status.Recording)
            {
                var totalBufferLength = Bars.Count() * SamplesPerBar;

                using (var frame = FrameOutputNode.GetFrame())
                {
                    using (var buffer = frame.LockBuffer(AudioBufferAccessMode.Write))
                    {
                        using (var reference = buffer.CreateReference())
                        {
                            ((IMemoryBufferByteAccess)reference).GetBuffer(out byte* unsafeBuffer, out uint numberOfBytes);
                            var numberOfSamples = (int)numberOfBytes / ElementSize;

                            if (position + numberOfSamples >= + totalBufferLength)
                            {
                                Stop();
                                return;
                            }

                            var barIndex = position / SamplesPerBar;
                            var bar = Bars[barIndex];

                            if (bar.Buffer == null)
                            {
                                bar.Buffer = new float[SamplesPerBar];
                            }

                            var offset = position % SamplesPerBar;

                            for (int i = 0; i < numberOfSamples; i++)
                            {
                                bar.Buffer[offset + i] = ((float*)unsafeBuffer)[i];
                            }

                            bar.EmitUpdate();
                        }
                    }
                }
            }
        }

        public unsafe void Read(int position, int numberOfSamples)
        {
            if (Status == Status.Playing)
            {
                if (numberOfSamples > 0)
                {
                    var totalBufferLength = Bars.Count() * SamplesPerBar;

                    if (position >= totalBufferLength)
                    {
                        Stop();
                        return;
                    }

                    var readBarIndex = position / (int)SamplesPerBar;
                    var readBar = Bars[readBarIndex];
                    var offset = position % SamplesPerBar;
                    var bufferSizeInBytes = numberOfSamples * ElementSize;

                    using (var frame = new AudioFrame((uint)bufferSizeInBytes))
                    {
                        using (var buffer = frame.LockBuffer(AudioBufferAccessMode.Write))
                        {
                            using (var reference = buffer.CreateReference())
                            {
                                ((IMemoryBufferByteAccess)reference).GetBuffer(out byte* dataInBytes, out uint capacityInBytes);
                                var capacity = (int)capacityInBytes / ElementSize;
                                var dataInFloat = (float*)dataInBytes;
                                for (int i = 0; i < capacity; i++)
                                {
                                    if (IsMuted || readBar.Buffer == null)
                                    {
                                        dataInFloat[i] = 0;
                                    }
                                    else
                                    {
                                        dataInFloat[i] = readBar.Buffer[i + offset];
                                    }
                                }
                            }
                        }

                        FrameInputNode.AddFrame(frame);
                    }
                }
            }
        }

        public void Record()
        {
            if (Status == Status.Stopped)
            {
                FrameOutputNode.Start();
                ChangeStatus(Status.Recording);
            }
        }

        public void Play()
        {
            if (Status == Status.Stopped)
            {
                FrameInputNode.Start();
                ChangeStatus(Status.Playing);
            }
        }

        public void Stop()
        {
            FrameInputNode.Stop();
            FrameOutputNode.Stop();
            ChangeStatus(Status.Stopped);
        }

        private void ChangeStatus(Status status)
        {
            Status = status;
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AddBar()
        {
            var model = new Model.Bar();
            Bars.Add(model);
            BarAdded?.Invoke(this, model);
        }
    }
}
