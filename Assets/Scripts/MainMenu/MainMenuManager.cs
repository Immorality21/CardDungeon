using Assets.Scripts.Audio;
using Assets.Scripts.MainMenu;
using ImmoralityGaming.Menu;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// The title screen (UI Toolkit): Continue, Options, Quit — and nothing else.
///
/// <para><b>It reads no save file, and that is the point.</b> Everything the player does between runs
/// moved to <c>HubScene</c> (<see cref="Assets.Scripts.Hub.HubManager"/>) on 2026-09-05, leaving this
/// document free of the managers, catalogs and save data the hub needs. That is what makes room for
/// the <b>save-slot picker</b> this screen is meant to grow: a slot picker cannot share a document
/// with screens that read the save it has not chosen yet. With one slot today, <b>Continue is that
/// choice</b> — pressing it opens the save file and walks into town.</para>
///
/// <para>The game does not come back here on its own. Both ways out of a dungeon load HubScene, so
/// the loop is hub → dungeon → hub; the only route to this screen is launching, or the town's
/// deliberate Main Menu button.</para>
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class MainMenuManager : MonoBehaviour
{
    [SerializeField] private UIDocument _document;

    private VisualElement _root;
    private VisualElement _titleView;
    private VisualElement _optionsView;

    private Button _continueButton;
    private Button _optionsButton;
    private Button _quitButton;

    private AudioOptionsUI _options;
    private KeyboardNavigator _nav;

    private void Start()
    {
        if (_document == null)
        {
            _document = GetComponent<UIDocument>();
        }
        _root = _document.rootVisualElement;

        _titleView = _root.Q<VisualElement>("title-view");
        _optionsView = _root.Q<VisualElement>("options-view");

        _continueButton = _root.Q<Button>("continue-btn");
        _optionsButton = _root.Q<Button>("options-btn");
        _quitButton = _root.Q<Button>("quit-btn");

        _continueButton.clicked += OnContinue;
        _optionsButton.clicked += OnOptions;
        if (_quitButton != null)
        {
            _quitButton.clicked += OnQuit;
        }

        _options = _optionsView != null ? new AudioOptionsUI(_optionsView) : null;
        if (_options != null)
        {
            _options.OnClosed += ShowTitle;
        }

        SetUpKeyboardNavigation();

        // The same bed the hub plays. Requesting the track already playing is a no-op, so walking
        // into town does not restart it.
        MusicPlayer.Play(MusicTrack.Hub);

        ShowTitle();
    }

    // --- keyboard --------------------------------------------------------------

    private void SetUpKeyboardNavigation()
    {
        _nav = new KeyboardNavigator(_root);
        _nav.Cancelled += OnNavigatorCancelled;

        _root.focusable = true;
        _root.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (_nav.HandleKey(evt))
            {
                evt.StopPropagation();
            }
        });

        // Swallow UI Toolkit's own focus navigation, or the first arrow moves focus off the root and
        // the second never arrives.
        _root.RegisterCallback<NavigationMoveEvent>(evt => evt.StopPropagation());
        _root.RegisterCallback<NavigationCancelEvent>(evt => evt.StopPropagation());
    }

    /// <summary>Escape presses the screen's own Back button, so keyboard and mouse take the same
    /// path — the options panel raises <c>OnClosed</c> from there.</summary>
    private void OnNavigatorCancelled()
    {
        if (IsShown(_optionsView))
        {
            KeyboardNavigator.Press(_root.Q<Button>("options-close"));
        }
    }

    /// <summary>See <c>PanelKeyboard</c>: a UITK panel only receives the OS keyboard while its event
    /// handler is the EventSystem's selection, and clicking the background clears it.</summary>
    private void Update()
    {
        PanelKeyboard.Claim();
    }

    private void ResetKeyboardNavigation()
    {
        _nav?.Reset();
        if (_root.panel != null)
        {
            _root.Focus();
        }
        PanelKeyboard.Claim();
    }

    // --- views ------------------------------------------------------------------

    private void ShowTitle()
    {
        SetShown(_titleView, true);
        SetShown(_optionsView, false);
        ResetKeyboardNavigation();
    }

    /// <summary>Opens the save file. With one slot that is unconditional; the picker that lands here
    /// later chooses <i>which</i> file first and then does exactly this.</summary>
    private void OnContinue()
    {
        SceneManager.LoadScene("HubScene");
    }

    private void OnOptions()
    {
        if (_options == null)
        {
            return;
        }
        SetShown(_titleView, false);
        _options.Show();
        ResetKeyboardNavigation();
    }

    private static void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static void SetShown(VisualElement element, bool shown)
    {
        if (element != null)
        {
            element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    private static bool IsShown(VisualElement element)
    {
        return element != null && element.resolvedStyle.display != DisplayStyle.None;
    }
}
