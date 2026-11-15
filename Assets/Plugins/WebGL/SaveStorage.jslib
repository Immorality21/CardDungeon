// Browser side of Assets/Scripts/IO/BrowserBridge.cs: keeping saves in IndexedDB, and moving a save
// file in and out of the browser (Options > Save data).
var SaveStoragePlugin = {

  // Copies Unity's in-memory /idbfs to IndexedDB. Calls that arrive while a sync is running are
  // folded into one more sync afterwards, so a burst of writes costs two syncs, not one per file.
  SaveStorage_Sync__deps: ['$FS'],
  SaveStorage_Sync: function () {
    if (typeof FS === 'undefined' || !FS.syncfs) {
      console.error('Immoral Dungeon: the browser file system is not available; saves will not survive a reload.');
      return;
    }
    var state = Module.cdSaveSync || (Module.cdSaveSync = { busy: false, again: false });
    var run = function () {
      state.busy = true;
      FS.syncfs(false, function (err) {
        if (err) {
          console.error('Immoral Dungeon: saving to browser storage failed', err);
        }
        if (state.again) {
          state.again = false;
          run();
        } else {
          state.busy = false;
        }
      });
    };
    if (state.busy) {
      state.again = true;
    } else {
      run();
    }
  },

  // Offers text to the player as a downloaded file.
  SaveStorage_Download: function (fileNamePtr, textPtr) {
    var fileName = UTF8ToString(fileNamePtr);
    var text = UTF8ToString(textPtr);
    var blob = new Blob([text], { type: 'application/json' });
    var url = URL.createObjectURL(blob);
    var link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.style.display = 'none';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    setTimeout(function () { URL.revokeObjectURL(url); }, 30000);
  },

  // Opens the browser's file picker and hands the chosen file's text to a C# method through
  // SendMessage (an empty string if it could not be read; nothing if the player cancels).
  //
  // Safari only opens a picker from inside the tap itself, and Unity handles a tap a frame later.
  // So the Import button arms the picker on press (SaveStorage_ArmPicker) and the release's own DOM
  // event opens it; the button's click then calls SaveStorage_PickFile, which only opens the picker
  // itself if the armed one did not (a keyboard press, or a tap too quick to arm in time).
  $SaveStoragePicker: {
    openedAt: 0,
    open: function (target, method) {
      SaveStoragePicker.openedAt = Date.now();
      var send = function (text) {
        if (typeof SendMessage === 'function') {
          SendMessage(target, method, text);
        } else {
          console.error('Immoral Dungeon: SendMessage is not available; the save file cannot be read.');
        }
      };
      var input = document.createElement('input');
      input.type = 'file';
      input.accept = '.json,application/json,text/plain';
      input.style.display = 'none';
      var remove = function () {
        if (input.parentNode) {
          input.parentNode.removeChild(input);
        }
      };
      input.addEventListener('cancel', remove);
      input.onchange = function () {
        var file = input.files && input.files[0];
        remove();
        if (!file) {
          return;
        }
        var reader = new FileReader();
        reader.onload = function () { send(String(reader.result)); };
        reader.onerror = function () { send(''); };
        reader.readAsText(file);
      };
      document.body.appendChild(input);
      input.click();
    },
    armed: null
  },

  SaveStorage_ArmPicker__deps: ['$SaveStoragePicker'],
  SaveStorage_ArmPicker: function (gameObjectPtr, methodPtr) {
    var target = UTF8ToString(gameObjectPtr);
    var method = UTF8ToString(methodPtr);
    if (SaveStoragePicker.armed) {
      document.removeEventListener('pointerup', SaveStoragePicker.armed, true);
    }
    var handler = function () {
      document.removeEventListener('pointerup', handler, true);
      SaveStoragePicker.armed = null;
      SaveStoragePicker.open(target, method);
    };
    SaveStoragePicker.armed = handler;
    document.addEventListener('pointerup', handler, true);
    // A press that turns into a drag, or never releases over the page, must not leave the picker
    // waiting for some later, unrelated tap.
    setTimeout(function () {
      if (SaveStoragePicker.armed === handler) {
        document.removeEventListener('pointerup', handler, true);
        SaveStoragePicker.armed = null;
      }
    }, 2000);
  },

  SaveStorage_PickFile__deps: ['$SaveStoragePicker'],
  SaveStorage_PickFile: function (gameObjectPtr, methodPtr) {
    if (Date.now() - SaveStoragePicker.openedAt < 1500) {
      return; // the armed picker already opened on this tap
    }
    if (SaveStoragePicker.armed) {
      document.removeEventListener('pointerup', SaveStoragePicker.armed, true);
      SaveStoragePicker.armed = null;
    }
    SaveStoragePicker.open(UTF8ToString(gameObjectPtr), UTF8ToString(methodPtr));
  },

  // Reloads the page once any pending save sync has finished, so an imported save is on disk
  // before the game restarts around it.
  SaveStorage_Reload: function () {
    var wait = function () {
      var state = Module.cdSaveSync;
      if (state && (state.busy || state.again)) {
        setTimeout(wait, 50);
      } else {
        window.location.reload();
      }
    };
    wait();
  }
};

autoAddDeps(SaveStoragePlugin, '$SaveStoragePicker');
mergeInto(LibraryManager.library, SaveStoragePlugin);
