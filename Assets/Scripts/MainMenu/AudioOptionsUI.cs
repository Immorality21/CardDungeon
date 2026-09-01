using System;
using Assets.Scripts.Audio;
using UnityEngine.UIElements;

namespace Assets.Scripts.MainMenu
{
    /// <summary>
    /// The hub's Options screen (UI Toolkit view-controller, not a MonoBehaviour — same shape as
    /// <see cref="MerchantUI"/>). Today it holds the audio dials: master, music and SFX, plus a mute
    /// toggle. Every change is written straight through <see cref="AudioOptions"/>, so it is applied
    /// and saved before the player has left the screen.
    ///
    /// <para>Each dial is a pair of stepped buttons around a readout rather than a slider. The hub's
    /// keyboard cursor navigates <em>buttons</em>, so a slider would be unreachable without a mouse —
    /// and the arrows already read naturally here: left/right nudges the dial the cursor is on.</para>
    /// </summary>
    public class AudioOptionsUI
    {
        private readonly VisualElement _root;
        private readonly Label _masterValue;
        private readonly Label _musicValue;
        private readonly Label _sfxValue;
        private readonly Button _muteButton;
        private readonly Label _note;
        private readonly Button _closeButton;

        public event Action OnClosed;

        public AudioOptionsUI(VisualElement root)
        {
            _root = root;
            _masterValue = root.Q<Label>("master-value");
            _musicValue = root.Q<Label>("music-value");
            _sfxValue = root.Q<Label>("sfx-value");
            _muteButton = root.Q<Button>("options-mute");
            _note = root.Q<Label>("options-note");
            _closeButton = root.Q<Button>("options-close");

            WireStep(root, "master-down", AudioChannel.Master, -1);
            WireStep(root, "master-up", AudioChannel.Master, 1);
            WireStep(root, "music-down", AudioChannel.Music, -1);
            WireStep(root, "music-up", AudioChannel.Music, 1);
            WireStep(root, "sfx-down", AudioChannel.Sfx, -1);
            WireStep(root, "sfx-up", AudioChannel.Sfx, 1);

            if (_muteButton != null)
            {
                _muteButton.clicked += () =>
                {
                    AudioOptions.ToggleMute();
                    Refresh();
                };
            }
            if (_closeButton != null)
            {
                _closeButton.clicked += Hide;
            }

            _root.style.display = DisplayStyle.None;
        }

        public void Show()
        {
            _root.style.display = DisplayStyle.Flex;
            Refresh();
        }

        public void Hide()
        {
            _root.style.display = DisplayStyle.None;
            OnClosed?.Invoke();
        }

        private void WireStep(VisualElement root, string buttonName, AudioChannel channel, int steps)
        {
            var button = root.Q<Button>(buttonName);
            if (button == null)
            {
                return; // an older scene's UXML — the screen degrades rather than throwing
            }
            button.clicked += () =>
            {
                AudioOptions.Nudge(channel, steps);
                // The confirm blip is the feedback for the SFX dial: you hear what you just set.
                if (channel != AudioChannel.Music)
                {
                    CombatAudio.Play(CombatSound.CursorMove);
                }
                Refresh();
            };
        }

        private void Refresh()
        {
            SetText(_masterValue, AudioOptions.Percent(AudioOptions.Get(AudioChannel.Master)));
            SetText(_musicValue, AudioOptions.Percent(AudioOptions.Get(AudioChannel.Music)));
            SetText(_sfxValue, AudioOptions.Percent(AudioOptions.Get(AudioChannel.Sfx)));

            bool muted = AudioOptions.Muted;
            if (_muteButton != null)
            {
                _muteButton.text = muted ? "Sound: Muted" : "Sound: On";
            }
            SetText(_note, muted
                ? "Everything is silenced. The dials keep their settings."
                : "Master scales everything; the other two scale their own kind on top of it.");
        }

        private static void SetText(Label label, string text)
        {
            if (label != null)
            {
                label.text = text;
            }
        }
    }
}
