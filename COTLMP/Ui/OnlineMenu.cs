/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     The online part of the main menu, in the game's own look: friends' lobbies and the lobby
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP.Debug;
using COTLMP.Game;
using COTLMP.Network;
using HarmonyLib;
using I2.Loc;
using Lamb.UI;
using Lamb.UI.MainMenu;
using Spine.Unity;
using src.UINavigator;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Ui
{
    /// <summary>
    /// The online menu and the lobby, built from the game's own main menu.
    /// </summary>
    /// <remarks>
    /// Both are copies of the game's save slot column (its header, buttons, highlight and fonts) on
    /// the main menu's own canvas, so they look like the rest of the menu and scale with it at any
    /// resolution. In the lobby the Lamb of the menu's key art steps aside for the Goat, a copy of it
    /// in the art's goat skin, and each wears its player's Steam name and avatar over its head.
    /// </remarks>
    [HarmonyPatch]
    internal static class OnlineMenu
    {
        /// <summary>
        /// A column of the menu, as the game's own submenus are
        /// </summary>
        internal sealed class Column : UISubmenuBase
        {
            internal Action Cancel, Tick;

            internal void Setup(CanvasGroup Group)
            {
                _canvasGroup = Group;
                _defaultSelectableFallbacks = new Selectable[0];
            }

            internal void Default(Selectable Button) => OverrideDefault(Button);

            internal void Focus()
            {
                if (_canvasGroup != null && _canvasGroup.interactable)
                    ActivateNavigation();
            }

            public override void OnCancelButtonInput()
            {
                if (_canvasGroup != null && _canvasGroup.interactable && gameObject.activeInHierarchy)
                    Cancel?.Invoke();
            }

            private void Update() => Tick?.Invoke();
        }

        private const int FriendRows = 4;

        private static Column Online, LobbyColumn;
        private static TMP_Text OnlineLabel, LobbyLine, LobbyNote;
        private static MMButton CreateButton, OnlineBack, PlayButton, InviteButton, LeaveButton;
        private static readonly MMButton[] FriendButtons = new MMButton[FriendRows];
        private static readonly (ulong Friend, ulong Lobby)[] FriendRowsShown = new (ulong, ulong)[FriendRows];
        private static float NextFriendsLook;
        private static string Joining;

        /* The key art's Lamb, the Goat beside it in the lobby, and their name tags */
        private static SkeletonAnimation LambArt, GoatArt;
        private static Vector3 LambHome;
        private static Tag LambTag, GoatTag;

        /* The menu's own font, for what the mod draws elsewhere */
        internal static TMP_FontAsset Font;
        internal static Material FontMaterial;

        /* The black screen with a line while a player waits for the other */
        private static GameObject Waiting;

        /// <summary>
        /// The menu is built (the main menu is up)
        /// </summary>
        internal static bool Ready => Online != null && LobbyColumn != null;

        /// <summary>
        /// The main menu's buttons show (past the title screen)
        /// </summary>
        internal static bool Shown => Ready && UIMainMenuController.Instance != null && UIMainMenuController.Instance.MainMenu.gameObject.activeInHierarchy;

        private static string T(string Key) => LocalizationManager.GetTranslation(Key);

        /* BUILDING ***********************************************************/

        [HarmonyPatch(typeof(UIMainMenuController), "Start")]
        [HarmonyPostfix]
        private static void Build(UIMainMenuController __instance)
        {
            try
            {
                Online = MakeColumn("COTLMP Online", out TMP_Text Header, out List<MMButton> Buttons, out OnlineLabel);
                Header.text = T("Multiplayer/Online/Title");
                CreateButton = Buttons[0];
                Label(CreateButton, T("Multiplayer/Online/CreateLobby"));
                CreateButton.onClick.AddListener(PickSave);
                for (int Index = 0; Index < FriendRows; Index++)
                {
                    int Row = Index;
                    FriendButtons[Index] = Buttons[1 + Index];
                    FriendButtons[Index].onClick.AddListener(() => JoinRow(Row));
                }
                /* Create, then the line over the friends' rows */
                CreateButton.transform.SetSiblingIndex(OnlineLabel.transform.GetSiblingIndex());
                OnlineBack = Buttons[5];
                Label(OnlineBack, T("Multiplayer/Online/Back"));
                OnlineBack.onClick.AddListener(BackToMain);
                Buttons[6].gameObject.SetActive(false);
                Online.Default(CreateButton);
                Online.Cancel = BackToMain;
                Online.Tick = TickOnline;

                LobbyColumn = MakeColumn("COTLMP Lobby", out Header, out Buttons, out LobbyLine);
                Header.text = T("Multiplayer/Lobby/Title");
                LobbyNote = Object.Instantiate(LobbyLine.gameObject, LobbyLine.transform.parent).GetComponent<TMP_Text>();
                LobbyNote.transform.SetSiblingIndex(LobbyLine.transform.GetSiblingIndex() + 1);
                PlayButton = Buttons[0];
                PlayButton.onClick.AddListener(Lobby.Play);
                InviteButton = Buttons[1];
                InviteButton.onClick.AddListener(SteamBridge.OpenInviteDialog);
                for (int Index = 2; Index < 5; Index++)
                    Buttons[Index].gameObject.SetActive(false);
                LeaveButton = Buttons[5];
                LeaveButton.onClick.AddListener(LeaveLobby);
                Buttons[6].gameObject.SetActive(false);
                LobbyColumn.Cancel = LeaveLobby;
                LobbyColumn.Tick = TickLobby;

                Font = Header.font;
                FontMaterial = Header.fontSharedMaterial;
                Plugin.Logger?.LogInfo("[LOBBY] Online menu built");
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogError($"[LOBBY] Can't build the online menu: {e}");
            }
        }

        /// <summary>
        /// A copy of the game's save slot column: its header, five slot buttons, Back and Delete,
        /// without what makes them save slots
        /// </summary>
        private static Column MakeColumn(string Name, out TMP_Text Header, out List<MMButton> Buttons, out TMP_Text Line)
        {
            LoadMenu Source = UIMainMenuController.Instance.LoadMenu;

            /* Copied under a switched off holder, so nothing of it wakes up before it is changed */
            GameObject Holder = new("COTLMP Holder");
            Holder.SetActive(false);
            GameObject Copy = Object.Instantiate(Source.gameObject, Holder.transform, false);
            Copy.name = Name;
            Object.DestroyImmediate(Copy.GetComponent<LoadMenu>());
            foreach (SaveSlotButton_BaseGame Slot in Copy.GetComponentsInChildren<SaveSlotButton_BaseGame>(true))
                Object.DestroyImmediate(Slot.gameObject);
            foreach (SaveSlotButtonBase Slot in Copy.GetComponentsInChildren<SaveSlotButtonBase>(true))
                Object.DestroyImmediate(Slot);
            List<GameObject> Badges = new();
            foreach (Transform Part in Copy.GetComponentsInChildren<Transform>(true))
            {
                if (Part.name.Contains("Completion Badge"))
                    Badges.Add(Part.gameObject);
            }
            Badges.ForEach(Object.DestroyImmediate);
            foreach (Localize Words in Copy.GetComponentsInChildren<Localize>(true))
                Object.DestroyImmediate(Words);

            Header = Copy.transform.Find("Play Header").GetComponent<TMP_Text>();
            Buttons = new List<MMButton>();
            foreach (Transform Child in Copy.transform)
            {
                MMButton Button = Child.GetComponent<MMButton>();
                if (Button == null)
                    continue;
                Button.onClick = new Button.ButtonClickedEvent();
                Buttons.Add(Button);
            }

            /* A line of text under the header, smaller and quieter */
            Line = Object.Instantiate(Header.gameObject, Copy.transform).GetComponent<TMP_Text>();
            Line.name = "Line";
            Line.transform.SetSiblingIndex(Header.transform.GetSiblingIndex() + 1);
            Line.fontSize = Header.fontSize * 0.75f;
            Line.color = new Color(0.36f, 0.33f, 0.3f, 1f);
            Line.enableWordWrapping = true;
            Line.text = "";

            Column Menu = Copy.AddComponent<Column>();
            Menu.Setup(Copy.GetComponent<CanvasGroup>());
            Copy.transform.SetParent(Source.transform.parent, false);
            Object.Destroy(Holder);

            /* Awake runs now and hides it, as the game's own columns are hidden at start */
            Copy.SetActive(true);
            return Menu;
        }

        private static void Label(MMButton Button, string Text)
        {
            TMP_Text Words = Button.GetComponentInChildren<TMP_Text>(true);
            if (Words != null)
                Words.text = Text;
        }

        /* MOVING BETWEEN COLUMNS *********************************************/

        /// <summary>
        /// The main menu's Multiplayer button
        /// </summary>
        internal static void Open()
        {
            if (!Ready)
            {
                Plugin.Logger?.LogError("[LOBBY] The online menu isn't built");
                return;
            }
            ShowOnline();
        }

        internal static void ShowOnline()
        {
            if (!Ready)
                return;
            Joining = null;
            NextFriendsLook = 0f;
            RestoreSlotsHeader();
            Go(Online);
        }

        internal static void ShowLobby()
        {
            if (!Ready)
                return;
            Refresh();
            Go(LobbyColumn);
            Stage(true);
            Plugin.MonoInstance.StartCoroutine(FocusLater(LobbyColumn));
        }

        /// <summary>
        /// The game starts from the lobby: the menu steps out of the way as the game's own does
        /// </summary>
        internal static void Leave()
        {
            Stage(false);
            if (LobbyColumn != null && LobbyColumn.gameObject.activeSelf)
                LobbyColumn.Hide();
        }

        [HarmonyPatch(typeof(UIMenuBase), nameof(UIMenuBase.Show))]
        [HarmonyPostfix]
        private static void MainShown(UIMenuBase __instance)
        {
            if (!Ready || UIMainMenuController.Instance == null || __instance != UIMainMenuController.Instance.MainMenu)
                return;
            foreach (Column Menu in new[] { Online, LobbyColumn })
            {
                if (Menu.gameObject.activeSelf && !Menu.IsHiding)
                {
                    Plugin.Logger?.LogInfo($"[LOBBY] The main menu came up over {Menu.name}, hiding it");
                    Menu.Hide(true);
                }
            }
            Stage(false);
        }

        private static void BackToMain()
        {
            Go(UIMainMenuController.Instance.MainMenu);
        }

        private static void PickSave()
        {
            Lobby.PickingSlot = true;
            TMP_Text Header = SlotsHeader();
            if (Header != null)
            {
                SlotsHeaderText ??= Header.text;
                Header.text = T("Multiplayer/Online/PickSave");
            }
            Go(UIMainMenuController.Instance.LoadMenu);
        }

        private static string SlotsHeaderText;

        private static TMP_Text SlotsHeader() => UIMainMenuController.Instance?.LoadMenu?.transform.Find("Play Header")?.GetComponent<TMP_Text>();

        private static void RestoreSlotsHeader()
        {
            TMP_Text Header = SlotsHeader();
            if (Header != null && SlotsHeaderText != null)
                Header.text = SlotsHeaderText;
            SlotsHeaderText = null;
        }

        private static void LeaveLobby()
        {
            if (Lobby.Open)
                Lobby.Close();
            else
                PauseMenuPatches.EndSession("Multiplayer/Steam/Left");
            ShowOnline();
        }

        private static void Go(UISubmenuBase To)
        {
            UIMainMenuController Controller = UIMainMenuController.Instance;
            UISubmenuBase From = Traverse.Create(Controller).Field("_currentMenu").GetValue<UIMenuBase>() as UISubmenuBase;
            if (To != LobbyColumn)
                Stage(false);
            if (From == To)
                To.Show();
            else
                AccessTools.Method(typeof(UIMainMenuController), "PerformMenuTransition").Invoke(Controller, new object[] { From, To });

            bool Main = To == Controller.MainMenu;
            object Prompts = Traverse.Create(Controller).Field("_controlPrompts").GetValue();
            if (Prompts != null)
                AccessTools.Method(Prompts.GetType(), Main ? "HideCancelButton" : "ShowCancelButton")?.Invoke(Prompts, null);
            if (Traverse.Create(Controller).Field("ad").GetValue() is CanvasGroup Ad)
                Ad.alpha = Main ? 1f : 0f;
        }

        /// <summary>
        /// The game's new game options close a moment after the lobby opens and leave the menus locked
        /// </summary>
        private static System.Collections.IEnumerator FocusLater(Column Menu)
        {
            yield return new WaitForSecondsRealtime(0.6f);
            MonoSingleton<UINavigatorNew>.Instance.LockInput = MonoSingleton<UINavigatorNew>.Instance.LockNavigation = false;
            if (Menu.CanvasGroup != null)
                Menu.CanvasGroup.interactable = true;
            Menu.Focus();
        }

        /* THE ONLINE COLUMN **************************************************/

        private static void TickOnline()
        {
            if (Time.unscaledTime < NextFriendsLook)
                return;
            NextFriendsLook = Time.unscaledTime + 2f;

            List<(ulong Friend, ulong Lobby)> Found = SteamBridge.FriendLobbies();
            for (int Index = 0; Index < FriendRows; Index++)
            {
                bool Shown = Index < Found.Count;
                FriendRowsShown[Index] = Shown ? Found[Index] : default;
                if (FriendButtons[Index].gameObject.activeSelf != Shown)
                    FriendButtons[Index].gameObject.SetActive(Shown);
                if (Shown)
                    Label(FriendButtons[Index], SteamBridge.NameOf(Found[Index].Friend));
            }

            OnlineLabel.text = Joining != null ? string.Format(T("Multiplayer/Online/Joining"), Joining) :
                               Found.Count > 0 ? T("Multiplayer/Online/FriendsLobbies") : T("Multiplayer/Online/NoLobbies");
        }

        private static void JoinRow(int Row)
        {
            (ulong Friend, ulong FriendLobby) = FriendRowsShown[Row];
            if (Friend == 0 || Plugin.GlobalsInternal.InGameSession)
                return;
            Joining = SteamBridge.NameOf(Friend);
            NextFriendsLook = 0f;
            SteamBridge.JoinFriend(Friend, FriendLobby);
        }

        /// <summary>
        /// The test harness: the joining copy goes in through the menu, as a player would
        /// </summary>
        internal static System.Collections.IEnumerator AutoJoin()
        {
            while (!Shown || MMTools.MMTransition.IsPlaying)
                yield return null;
            yield return new WaitForSecondsRealtime(4f);
            Open();
            float Until = Time.realtimeSinceStartup + 60f;
            while (FriendRowsShown[0].Friend == 0 && Time.realtimeSinceStartup < Until)
                yield return null;
            yield return new WaitForSecondsRealtime(LobbyShots.Dir != null ? 3f : 0.5f);
            yield return LobbyShots.Take("online");
            Plugin.Logger?.LogWarning("[QA client] Joining the first friend's lobby from the online menu");
            JoinRow(0);
            Until = Time.realtimeSinceStartup + 60f;
            while (!Lobby.Waiting && Time.realtimeSinceStartup < Until)
                yield return null;
            yield return new WaitForSecondsRealtime(3f);
            if (Lobby.Waiting)
                yield return LobbyShots.Take("lobby");
        }

        /* THE LOBBY COLUMN ***************************************************/

        /// <summary>
        /// The lobby's lines and buttons for who we are
        /// </summary>
        internal static void Refresh()
        {
            if (!Ready)
                return;
            bool Host = Lobby.Open;
            ulong HostId = Host ? SteamBridge.LocalId : SteamBridge.JoinedHost;
            LobbyLine.text = Host ? Lobby.SaveLine : string.Format(T("Multiplayer/Lobby/HostSave"), SteamBridge.NameOf(HostId), Lobby.SaveLine);
            LobbyNote.text = Host ? "" : T("Multiplayer/Lobby/WaitingHost");
            PlayButton.gameObject.SetActive(Host);
            Label(PlayButton, T("Multiplayer/Lobby/Play"));
            InviteButton.gameObject.SetActive(Host && !SteamBridge.IsFake);
            Label(InviteButton, T("Multiplayer/Lobby/Invite"));
            Label(LeaveButton, T(Host ? "Multiplayer/Lobby/Close" : "Multiplayer/Lobby/Leave"));
            LobbyColumn.Default(Host ? PlayButton : LeaveButton);
        }

        private static void TickLobby()
        {
            if (!Lobby.Open && !Lobby.Waiting)
                return;
            ulong Host = Lobby.Open ? SteamBridge.LocalId : SteamBridge.JoinedHost;
            List<ulong> Partners = SteamBridge.PartnerIds();
            ulong Friend = Lobby.Open ? (Partners.Count > 0 ? Partners[0] : 0) : SteamBridge.LocalId;
            LambTag?.Show(Host, true, null);
            GoatTag?.Show(Friend, false, Friend == 0 ? T("Multiplayer/Lobby/WaitingFriend") : null);
            if (GoatArt != null && GoatArt.Skeleton != null)
                GoatArt.Skeleton.A = Mathf.MoveTowards(GoatArt.Skeleton.A, Friend == 0 ? 0.3f : 1f, Time.unscaledDeltaTime * 3f);
        }

        /* THE LAMB AND THE GOAT **********************************************/

        private const float Apart = 4.6f;

        private static void Stage(bool On)
        {
            try
            {
                if (On && GoatArt == null)
                {
                    MainMenuController Scene = Object.FindObjectOfType<MainMenuController>();
                    LambArt = Scene != null ? Traverse.Create(Scene).Field("lambSpine").GetValue<SkeletonAnimation>() : null;
                    if (LambArt == null)
                        return;
                    LambHome = LambArt.transform.localPosition;
                    GoatArt = Object.Instantiate(LambArt.gameObject, LambArt.transform.parent).GetComponent<SkeletonAnimation>();
                    GoatArt.name = "COTLMP Goat";
                    GoatArt.Skeleton.SetSkin("goat");
                    GoatArt.Skeleton.SetSlotsToSetupPose();
                    GoatArt.AnimationState.SetAnimation(0, LambArt.AnimationState.GetCurrent(0)?.Animation?.Name ?? "animation", true).TrackTime = 0.9f;
                    /* Closer on a narrow screen, where the menu's column comes nearer */
                    float Spread = Apart * Mathf.Clamp((float)Screen.width / Screen.height / (16f / 9f), 0.72f, 1f);
                    GoatArt.transform.localPosition = LambHome + new Vector3(Spread, 0f, 0f);
                    LambArt.transform.localPosition = LambHome - new Vector3(Spread, 0f, 0f);
                    GoatArt.Skeleton.A = 0.3f;

                    RectTransform Canvas = (RectTransform)UIMainMenuController.Instance.transform;
                    LambTag = new Tag(Canvas, LambArt);
                    GoatTag = new Tag(Canvas, GoatArt);
                }
                else if (!On && GoatArt != null)
                {
                    Object.Destroy(GoatArt.gameObject);
                    GoatArt = null;
                    if (LambArt != null)
                        LambArt.transform.localPosition = LambHome;
                    LambTag?.Destroy();
                    GoatTag?.Destroy();
                    LambTag = GoatTag = null;
                }
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"[LOBBY] The lobby's stage: {e.Message}");
            }
        }

        /// <summary>
        /// A player's Steam avatar and name over their character's head, on a ribbon of the menu's own
        /// </summary>
        private sealed class Tag
        {
            private const float Height = 56f, Picture = 44f;

            private readonly GameObject Root;
            private readonly RectTransform Rect;
            private readonly RawImage Avatar;
            private readonly TMP_Text Name;
            private readonly SkeletonAnimation Over;
            private float[] Vertices = new float[8];
            private readonly RectTransform Canvas;
            private ulong ShownId = ulong.MaxValue;
            private string ShownText;

            internal Tag(RectTransform OnCanvas, SkeletonAnimation Character)
            {
                Canvas = OnCanvas;
                Over = Character;
                Image Highlight = OnCanvas.GetComponentsInChildren<Image>(true).FirstOrDefault(Item => Item.sprite != null && Item.sprite.name == "Button Black");

                Image Band = new GameObject("COTLMP Tag", typeof(RectTransform)).AddComponent<Image>();
                Root = Band.gameObject;
                Rect = Band.rectTransform;
                Rect.SetParent(OnCanvas, false);
                Rect.anchorMin = Rect.anchorMax = new Vector2(0.5f, 0.5f);
                Rect.pivot = new Vector2(0.5f, 0f);
                Rect.sizeDelta = new Vector2(240f, Height);
                if (Highlight != null)
                {
                    Band.sprite = Highlight.sprite;
                    Band.material = Highlight.material;
                }
                else
                {
                    Band.color = new Color(0.08f, 0.07f, 0.07f, 1f);
                }

                Avatar = new GameObject("Avatar", typeof(RectTransform)).AddComponent<RawImage>();
                Avatar.transform.SetParent(Rect, false);
                RectTransform AvatarRect = Avatar.rectTransform;
                AvatarRect.anchorMin = AvatarRect.anchorMax = new Vector2(0f, 0.5f);
                AvatarRect.pivot = new Vector2(0f, 0.5f);
                AvatarRect.anchoredPosition = new Vector2(30f, 2f);
                AvatarRect.sizeDelta = new Vector2(Picture, Picture);

                Name = new GameObject("Name", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                Name.transform.SetParent(Rect, false);
                RectTransform NameRect = Name.rectTransform;
                NameRect.anchorMin = Vector2.zero;
                NameRect.anchorMax = Vector2.one;
                NameRect.offsetMax = new Vector2(-30f, 0f);
                if (Font != null)
                {
                    Name.font = Font;
                    Name.fontSharedMaterial = FontMaterial;
                }
                Name.fontSize = 28f;
                Name.alignment = TextAlignmentOptions.Center;
                Name.color = new Color(0.961f, 0.929f, 0.835f, 1f);
                Name.enableWordWrapping = false;
                Name.overflowMode = TextOverflowModes.Ellipsis;

                Root.AddComponent<Follow>().Owner = this;
            }

            /// <summary>
            /// The player (0 for nobody yet: the line instead)
            /// </summary>
            internal void Show(ulong Id, bool Lamb, string Line)
            {
                Texture2D Face = Id == 0 ? null : SteamAvatars.Of(Id, Lamb);
                string Text = Line ?? SteamBridge.NameOf(Id);
                Avatar.texture = Face;
                Avatar.gameObject.SetActive(Face != null);
                float Left = Face != null ? 30f + Picture + 10f : 30f;
                if (Id != ShownId || Text != ShownText || Name.rectTransform.offsetMin.x != Left)
                {
                    ShownId = Id;
                    ShownText = Text;
                    Name.text = Text;
                    Name.rectTransform.offsetMin = new Vector2(Left, 0f);
                    Rect.sizeDelta = new Vector2(Mathf.Clamp(Name.GetPreferredValues(Text).x + Left + 34f, 150f, 440f), Height);
                }
            }

            internal void Place()
            {
                if (Over == null)
                    return;
                Camera Eye = UIMainMenuController.Instance != null && UIMainMenuController.Instance.Canvas != null ? UIMainMenuController.Instance.Canvas.worldCamera : Camera.main;
                if (Eye == null)
                    return;
                /* The top of what the skeleton draws (the triangle over the crown), in the world */
                if (Over.Skeleton == null)
                    return;
                Over.Skeleton.GetBounds(out float X, out float Y, out float Width, out float Height, ref Vertices);
                Vector3 Screen = Eye.WorldToScreenPoint(Over.transform.TransformPoint(new Vector3(X + Width / 2f, Y + Height, 0f)));
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(Canvas, Screen, Eye, out Vector2 Local))
                    Rect.anchoredPosition = Local + new Vector2(0f, 16f);
            }

            internal void Destroy() => Object.Destroy(Root);
        }

        private sealed class Follow : MonoBehaviour
        {
            internal Tag Owner;

            private void LateUpdate() => Owner?.Place();
        }

        /* NOTES AND WAITING **************************************************/

        internal static void Tell(string Key)
        {
            try
            {
                NotificationCentre.Instance?.PlayGenericNotification(Key);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// A black screen with this line over everything, or none for null
        /// </summary>
        internal static void Wait(string Key)
        {
            if (Key == null)
            {
                if (Waiting != null)
                    Object.Destroy(Waiting);
                Waiting = null;
                return;
            }
            if (Waiting == null)
            {
                Waiting = new GameObject("COTLMP Waiting");
                Object.DontDestroyOnLoad(Waiting);
                Canvas Paper = Waiting.AddComponent<Canvas>();
                Paper.renderMode = RenderMode.ScreenSpaceOverlay;
                Paper.sortingOrder = 32000;
                CanvasScaler Scaler = Waiting.AddComponent<CanvasScaler>();
                Scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                Scaler.referenceResolution = new Vector2(1920f, 1080f);
                Scaler.matchWidthOrHeight = 0.5f;

                Image Black = new GameObject("Black", typeof(RectTransform)).AddComponent<Image>();
                Black.transform.SetParent(Waiting.transform, false);
                Black.color = Color.black;
                Black.rectTransform.anchorMin = Vector2.zero;
                Black.rectTransform.anchorMax = Vector2.one;
                Black.rectTransform.offsetMin = Black.rectTransform.offsetMax = Vector2.zero;

                TextMeshProUGUI Words = new GameObject("Line", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                Words.transform.SetParent(Waiting.transform, false);
                Words.rectTransform.anchorMin = new Vector2(0.1f, 0.4f);
                Words.rectTransform.anchorMax = new Vector2(0.9f, 0.6f);
                Words.rectTransform.offsetMin = Words.rectTransform.offsetMax = Vector2.zero;
                if (Font != null)
                {
                    Words.font = Font;
                    Words.fontSharedMaterial = FontMaterial;
                }
                Words.fontSize = 40f;
                Words.alignment = TextAlignmentOptions.Center;
                Words.color = new Color(0.961f, 0.929f, 0.835f, 1f);
            }
            Waiting.GetComponentInChildren<TMP_Text>().text = T(Key);
        }
    }
}

/* EOF */
