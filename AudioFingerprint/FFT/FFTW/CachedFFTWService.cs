namespace AudioFingerprint.FFT.FFTW
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;

    internal class CachedFFTWService : FFTWService
    {
        private readonly FFTWService fftwService;

        private readonly object lockObject = new object();

        private Dictionary<int, List<FFTWArray>> memory = new Dictionary<int, List<FFTWArray>>(); // cache for in, out, plan arrays (in order not to allocate unmanaged memory on every call)

        private bool alreadyDisposed;

        public CachedFFTWService(FFTWService fftwService)
        {
            this.fftwService = fftwService;
        }

        ~CachedFFTWService()
        {
            Dispose(false);
        }

        public override float[] FFTForward(float[] signal, int startIndex, int length)
        {
            FFTWArray fftw;
            lock (lockObject)
            {
                if (memory.ContainsKey(length))
                {
                    List<FFTWArray> items = memory[length];
                    fftw = items[0];
                    items.RemoveAt(0);
                    if (items.Count <= 0)
                    {
                        memory.Remove(length);
                    }
                }
                else
                {
                    fftw = new FFTWArray();
                    fftw.Input = GetInput(length);
                    fftw.Output = GetOutput(length);
                    fftw.Plan = GetFFTPlan(length, fftw.Input, fftw.Output);
                }
            } //lock

            float[] applyTo = new float[length];
            Array.Copy(signal, startIndex, applyTo, 0, length);
            Marshal.Copy(applyTo, 0, fftw.Input, length);
            Execute(fftw.Plan);
            float[] result = new float[length * 2];
            Marshal.Copy(fftw.Output, result, 0, length);

            lock (lockObject)
            {
                // Voeg dit plan weer toe aan de cache
                if (memory.ContainsKey(length))
                {
                    memory[length].Add(fftw);
                }
                else
                {
                    List<FFTWArray> items = new List<FFTWArray>();
                    items.Add(fftw);
                    memory.Add(length, items);
                }
            } //lock

            return result;
        }

        public override IntPtr GetInput(int length)
        {
            return fftwService.GetInput(length);
        }

        public override IntPtr GetOutput(int length)
        {
            return fftwService.GetOutput(length);
        }

        public override void FreeUnmanagedMemory(IntPtr memoryBlock)
        {
            // do nothing
        }

        public override void FreePlan(IntPtr fftPlan)
        {
            // do nothing    
        }

        public override void Execute(IntPtr fftPlan)
        {
            fftwService.Execute(fftPlan);
        }

        public override IntPtr GetFFTPlan(int length, IntPtr input, IntPtr output)
        {
            return fftwService.GetFFTPlan(length, input, output);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (!alreadyDisposed)
            {
                alreadyDisposed = true;
                if (isDisposing)
                {
                    // release managed resources
                }

                lock (lockObject)
                {
                    foreach (KeyValuePair<int, List<FFTWArray>> entry in memory)
                    {
                        foreach (FFTWArray item in entry.Value)
                        {
                            fftwService.FreeUnmanagedMemory(item.Input);
                            fftwService.FreeUnmanagedMemory(item.Output);
                            fftwService.FreePlan(item.Plan);
                        } //foreach
                    } //foreach

                    memory.Clear();
                } //lock
            }
        }
    }
}
