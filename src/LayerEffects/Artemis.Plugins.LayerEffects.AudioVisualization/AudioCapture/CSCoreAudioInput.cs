﻿using System;
using CSCore;
using CSCore.CoreAudioAPI;
using CSCore.SoundIn;
using CSCore.Streams;

namespace Artemis.Plugins.LayerEffects.AudioVisualization.AudioCapture
{
    public class CSCoreAudioInput : IAudioInput
    {
        #region Properties & Fields

        private WasapiCapture _capture;
        private SoundInSource _soundInSource;
        private IWaveSource _source;
        private SingleBlockNotificationStream _stream;
        private AudioEndpointVolume _audioEndpointVolume;

        public int SampleRate => _soundInSource?.WaveFormat?.SampleRate ?? -1;
        public float MasterVolume => _audioEndpointVolume.MasterVolumeLevelScalar;

        #endregion

        #region Event

        public event AudioData DataAvailable;

        #endregion

        #region Methods

        public void Initialize()
        {
            MMDevice captureDevice = MMDeviceEnumerator.DefaultAudioEndpoint(DataFlow.Render, Role.Console);
            WaveFormat deviceFormat = captureDevice.DeviceFormat;
            _audioEndpointVolume = AudioEndpointVolume.FromDevice(captureDevice);

            //DarthAffe 07.02.2018: This is a really stupid workaround to (hopefully) finally fix the surround driver issues
            for (int i = 1; i < 13; i++)
                try { _capture = new WasapiLoopbackCapture(100, new WaveFormat(deviceFormat.SampleRate, deviceFormat.BitsPerSample, i)); } catch { /* We're just trying ... */ }

            if (_capture == null)
                throw new NullReferenceException("Failed to initialize WasapiLoopbackCapture");

            _capture.Initialize();

            _soundInSource = new SoundInSource(_capture) { FillWithZeros = false };
            _source = _soundInSource.WaveFormat.SampleRate == 44100
                          ? _soundInSource.ToStereo()
                          : _soundInSource.ChangeSampleRate(44100).ToStereo();

            _stream = new SingleBlockNotificationStream(_source.ToSampleSource());
            _stream.SingleBlockRead += StreamOnSingleBlockRead;

            _source = _stream.ToWaveSource();

            byte[] buffer = new byte[_source.WaveFormat.BytesPerSecond / 2];
            _soundInSource.DataAvailable += (s, aEvent) =>
                                            {
                                                while ((_source.Read(buffer, 0, buffer.Length)) > 0) ;
                                            };

            _capture.Start();
        }

        public void Dispose()
        {
            _capture?.Stop();
            _capture?.Dispose();
        }

        private void StreamOnSingleBlockRead(object sender, SingleBlockReadEventArgs singleBlockReadEventArgs)
            => DataAvailable?.Invoke(singleBlockReadEventArgs.Left, singleBlockReadEventArgs.Right);

        #endregion
    }
}
