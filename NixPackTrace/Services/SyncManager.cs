using System;
using System.Threading.Tasks;
using NixPackTrace.Data;

namespace NixPackTrace.Services
{
    /// <summary>
    /// Background service that retries any locally-saved records
    /// that failed to sync with Firebase (SYNC_STATUS = 'Pending').
    /// Runs on a 30-second loop.
    /// </summary>
    public class SyncManager
    {
        private readonly LocalDbService   _localDb;
        private readonly FirebaseService  _firebase;
        private bool _running;

        public SyncManager(LocalDbService localDb, FirebaseService firebase)
        {
            _localDb  = localDb;
            _firebase = firebase;
        }

        public void Start() { if (!_running) { _running = true; Task.Run(LoopAsync); } }
        public void Stop()  { _running = false; }

        private async Task LoopAsync()
        {
            while (_running)
            {
                await Task.Delay(TimeSpan.FromSeconds(30));
                try
                {
                    var pending = await _localDb.GetPendingSyncRecordsAsync();
                    foreach (var r in pending)
                    {
                        bool ok = await _firebase.UpdatePackingAsync(r);
                        if (ok) await _localDb.MarkAsSyncedAsync(r.MAC_ID);
                    }

                    var pendingDispatches = await _localDb.GetPendingDispatchRecordsAsync();
                    foreach (var d in pendingDispatches)
                    {
                        bool ok = await _firebase.UpdateDispatchAsync(d);
                        if (ok) await _localDb.MarkDispatchAsSyncedAsync(d.DispatchId);
                    }
                }
                catch { /* never crash the background loop */ }
            }
        }
    }
}
