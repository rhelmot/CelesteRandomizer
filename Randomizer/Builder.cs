using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Celeste.Mod.Randomizer
{
    public class Builder : IDisposable
    {

        /// <summary>
        /// Possible Builder statuses. Values are ardered by priority so status may only increase in value, so numeric comparisons may be used.
        /// </summary>
        public enum BuilderStatus
        {
            NotStarted = 0,
            WaitingForThread = 10,
            Running = 20,
            Success = 30,
            Error = 40,
            Abort = 50,
            Disposed = 99999,
        }

        /// <summary>
        /// Callback to execute on success
        /// </summary>
        public Action<AreaKey> OnSuccess { get; set; }

        /// <summary>
        /// Callback to execute on error
        /// </summary>
        public Action<Exception> OnError { get; set; }

        /// <summary>
        /// Callback to execute on abort
        /// </summary>
        public Action OnAbort { get; set; }

        /// <summary>
        /// The status of the worker. For thread safety, threadLock is required when setting.
        /// </summary>
        public BuilderStatus Status
        {
            get
            {
                lock (threadLock)
                {
                    return _status;
                }
            }
            private set
            {
                if (value > _status) _status = value;
            }
        }

        /// <summary>
        /// Generic object used as the lock target for thread-safe operation
        /// </summary>
        private object threadLock = new object();

        private Thread worker = null;
        private RandoSettings settings;
        private volatile BuilderStatus _status = BuilderStatus.NotStarted;
        private volatile Exception exception = null;
        private AreaKey? generatedArea = null;

        public Builder() { }

        /// <summary>
        /// Begin building
        /// </summary>
        public void Go(RandoSettings settings)
        {
            lock (threadLock)
            {
                if (Status > BuilderStatus.WaitingForThread) return;
                Status = BuilderStatus.WaitingForThread;
            }
            this.settings = settings.Copy();
            worker = new Thread(BackgroundThreadMain) { IsBackground = true };
            worker.Start();
        }

        /// <summary>
        /// Abort the build if it has been started and dispose of resources
        /// </summary>
        public void Abort()
        {
            lock (threadLock)
            {
                if (Status == BuilderStatus.Disposed) return;
                worker?.Abort();
                Status = BuilderStatus.Disposed;
            }
        }

        /// <summary>
        /// Call this on the main thread to check its status and trigger OnSuccess/OnError/OnAbort events if necessary.
        /// </summary>
        /// <returns>True if the builder is finished and can be disposed.</returns>
        public bool Check()
        {
            lock (threadLock)
            {
                BuilderStatus status = Status;
                if (status < BuilderStatus.Success || status == BuilderStatus.Disposed) return false;
                RandoModule.Instance.SavedData.SavedSettings = settings;
                RandoModule.Instance.SaveSettings();
                switch (status)
                {
                    case BuilderStatus.Success:
                        OnSuccess?.Invoke(generatedArea.Value);
                        break;
                    case BuilderStatus.Error:
                        OnError?.Invoke(exception);
                        break;
                    case BuilderStatus.Abort:
                        OnAbort?.Invoke();
                        break;
                    default:
                        Logger.Log(LogLevel.Error, "Randomizer", $"Randomizer builder encountered a mysetry status: '{status}'");
                        break;
                }
                Dispose();
                return true;
            }
        }

        /// <summary>
        /// Dispose of resources
        /// </summary>
        public void Dispose()
        {
            lock (threadLock)
            {
                if (Status == BuilderStatus.Disposed) return;
                worker?.Abort();
                Status = BuilderStatus.Disposed;
            }
        }

        /// <summary>
        /// Main function for the worker thread. Performs the map build.
        /// </summary>
        private void BackgroundThreadMain()
        {
            Status = BuilderStatus.Running;
            settings.Enforce();
            AreaKey newArea;
            try
            {
                newArea = RandoLogic.GenerateMap(settings);
            }
            catch (ThreadAbortException)
            {
                lock (threadLock)
                {
                    Status = BuilderStatus.Abort;
                    worker = null;
                }
                return;
            }
            catch (Exception e)
            {
                lock (threadLock)
                {
                    exception = e;
                    Status = BuilderStatus.Error;
                    worker = null;
                }
                return;
            }
            lock (threadLock)
            {
                generatedArea = newArea;
                Status = BuilderStatus.Success;
                worker = null;
            }
        }
    }
}
