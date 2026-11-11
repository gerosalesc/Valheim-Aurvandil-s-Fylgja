using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace StandsOfValheim
{
    /// <summary>
    /// Loads and caches custom audio clips for JoJo Stand abilities.
    /// Supports embedded assembly resources and override files on disk.
    /// </summary>
    internal static class SoundManager
    {
        private static AudioClip s_oraClip;

        public static AudioClip OraClip
        {
            get
            {
                if (s_oraClip == null)
                {
                    s_oraClip = LoadOraClip();
                }
                return s_oraClip;
            }
        }

        private static AudioClip LoadOraClip()
        {
            try
            {
                // 1. Check for custom audio file placed next to DLL
                string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(dllDir))
                {
                    string[] fileNames = { "ora_barrage.wav", "ora.wav", Path.Combine("resources", "ora_barrage.wav") };
                    foreach (var name in fileNames)
                    {
                        string fullPath = Path.Combine(dllDir, name);
                        if (File.Exists(fullPath))
                        {
                            byte[] bytes = File.ReadAllBytes(fullPath);
                            var clip = CreateClipFromWav(bytes, "OraBarrage_Disk");
                            if (clip != null)
                            {
                                Plugin.Log?.LogInfo($"SoundManager: Loaded ORA audio from disk: {fullPath}");
                                return clip;
                            }
                        }
                    }
                }

                // 2. Load embedded resource from DLL
                var asm = Assembly.GetExecutingAssembly();
                string[] resNames = asm.GetManifestResourceNames();
                foreach (var res in resNames)
                {
                    if (res.EndsWith("ora_barrage.wav", StringComparison.OrdinalIgnoreCase))
                    {
                        using (var stream = asm.GetManifestResourceStream(res))
                        {
                            if (stream != null)
                            {
                                byte[] bytes = new byte[stream.Length];
                                stream.Read(bytes, 0, bytes.Length);
                                var clip = CreateClipFromWav(bytes, "OraBarrage_Embedded");
                                if (clip != null)
                                {
                                    Plugin.Log?.LogInfo($"SoundManager: Loaded embedded ORA audio: {res} ({bytes.Length} bytes)");
                                    return clip;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"SoundManager: Failed to load ORA audio: {ex.Message}");
            }

            return null;
        }

        public static AudioClip CreateClipFromWav(byte[] bytes, string clipName)
        {
            try
            {
                if (bytes == null || bytes.Length < 44) return null;

                int channels = BitConverter.ToInt16(bytes, 22);
                int sampleRate = BitConverter.ToInt32(bytes, 24);
                int bitsPerSample = BitConverter.ToInt16(bytes, 34);

                int dataIndex = 12;
                while (dataIndex < bytes.Length - 4)
                {
                    if (bytes[dataIndex] == 'd' && bytes[dataIndex + 1] == 'a' && bytes[dataIndex + 2] == 't' && bytes[dataIndex + 3] == 'a')
                    {
                        dataIndex += 4;
                        break;
                    }
                    dataIndex++;
                }

                if (dataIndex >= bytes.Length - 4) return null;

                int dataSize = BitConverter.ToInt32(bytes, dataIndex);
                dataIndex += 4;

                int bytesPerSample = bitsPerSample / 8;
                int totalSamples = dataSize / bytesPerSample;
                float[] floatSamples = new float[totalSamples];

                if (bitsPerSample == 16)
                {
                    for (int i = 0; i < totalSamples; i++)
                    {
                        int byteOffset = dataIndex + i * 2;
                        if (byteOffset + 1 < bytes.Length)
                        {
                            short val = BitConverter.ToInt16(bytes, byteOffset);
                            floatSamples[i] = val / 32768f;
                        }
                    }
                }
                else if (bitsPerSample == 8)
                {
                    for (int i = 0; i < totalSamples; i++)
                    {
                        floatSamples[i] = (bytes[dataIndex + i] - 128) / 128f;
                    }
                }

                int sampleCountPerChannel = totalSamples / channels;
                var clip = AudioClip.Create(clipName, sampleCountPerChannel, channels, sampleRate, false);
                typeof(AudioClip).GetMethod("SetData", new Type[] { typeof(float[]), typeof(int) })
                                 .Invoke(clip, new object[] { floatSamples, 0 });
                return clip;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"SoundManager: Error parsing WAV data: {ex.Message}");
                return null;
            }
        }
    }
}
