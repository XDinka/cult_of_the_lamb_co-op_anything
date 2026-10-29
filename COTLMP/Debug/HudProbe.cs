/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Test harness: frames and a written account of the HUD, and the game's own co-op to compare with
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using MMTools;
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/* CLASSES & CODE *************************************************************/

namespace COTLMP.Debug
{
    /// <summary>
    /// What the HUD shows, for comparing ours with the game's own co-op.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><c>-cotlmp-hudshots=DIR</c>: a few seconds into every scene and room (and every 30 s in one), a frame and
    /// a text file: the players' real hearts, each hearts panel (whose, its hearts and their skins, the co-op ring),
    /// and every HUD element that is on screen</item>
    /// <item><c>-cotlmp-vanillacoop</c>: no session; in the cult the game's own second player joins (as a second pad
    /// would), then both go into the first dungeon: the game's co-op HUD to compare with</item>
    /// <item><c>-cotlmp-menushot</c>: with -cotlmp-hudshots, past the title screen by itself and a frame of the main menu</item>
    /// <item><c>-cotlmp-onlineshot</c>: with -cotlmp-menushot, the mod's online menu opened and framed</item>
    /// <item><c>-cotlmp-langshots</c>: with -cotlmp-menushot, in the main menu each of the game's 15 languages in turn (in
    /// memory only: the game's settings and its remembered language stay as they are), the partner's line box drawn
    /// with a line of the game's and one of the mod's, and a frame of each</item>
    /// <item><c>-cotlmp-holdtest</c>: the host, once the friend stands with it in a dungeon room, goes on to the room next
    /// door alone, starts a conversation there (it must wait for the friend) and tries a door (it must stay shut); the
    /// friend (with <c>-cotlmp-solo</c>) comes a few seconds later and the conversation starts</item>
    /// <item><c>-cotlmp-geartest</c>: the friend, the first time in a dungeon: an axe of level 2, the tentacles curse, the
    /// Moon card, a hit, chores level 3; the host saves its game a minute after (the friends' characters are written
    /// with it). With <c>-cotlmp-rejoin</c> the friend comes back to see it all put back</item>
    /// </list>
    /// </remarks>
    internal static class HudProbe
    {
        private const string Tag = "[QA]";
        private static string Dir;
        private static bool VanillaCoop, GearTest, HoldTest, MenuShot, TitleShot, LangShots, LogTest, OnlineShot;
        private static int Count;

        internal static void Initialize()
        {
            foreach (string Arg in Environment.GetCommandLineArgs())
            {
                if (Arg.StartsWith("-cotlmp-hudshots=", StringComparison.OrdinalIgnoreCase))
                    Dir = Arg.Substring("-cotlmp-hudshots=".Length);
                else if (Arg.Equals("-cotlmp-vanillacoop", StringComparison.OrdinalIgnoreCase))
                    VanillaCoop = true;
                else if (Arg.Equals("-cotlmp-geartest", StringComparison.OrdinalIgnoreCase))
                    GearTest = true;
                else if (Arg.Equals("-cotlmp-holdtest", StringComparison.OrdinalIgnoreCase))
                    HoldTest = true;
                else if (Arg.Equals("-cotlmp-menushot", StringComparison.OrdinalIgnoreCase))
                    MenuShot = true;
                else if (Arg.Equals("-cotlmp-titleshot", StringComparison.OrdinalIgnoreCase))
                    TitleShot = true;
                else if (Arg.Equals("-cotlmp-langshots", StringComparison.OrdinalIgnoreCase))
                    LangShots = true;
                else if (Arg.Equals("-cotlmp-onlineshot", StringComparison.OrdinalIgnoreCase))
                    OnlineShot = true;
                else if (Arg.Equals("-cotlmp-logtest", StringComparison.OrdinalIgnoreCase))
                    LogTest = COTLMP.Ui.LogButton.Quiet = true;
            }
            if (GearTest)
                Plugin.MonoInstance.StartCoroutine(Gear());
            if (HoldTest)
                Plugin.MonoInstance.StartCoroutine(Hold());
            if (TitleShot && Dir != null)
                Plugin.MonoInstance.StartCoroutine(TitleFrame());
            if (MenuShot && Dir != null)
            {
                PressStart.On = true;
                Plugin.MonoInstance.StartCoroutine(Menu());
            }
            if (Dir != null)
                Plugin.MonoInstance.StartCoroutine(Watch());
            if (VanillaCoop)
                Plugin.MonoInstance.StartCoroutine(PlayGameCoop());
        }

        private static string Role => VanillaCoop ? "vanilla" : Plugin.GlobalsInternal.IsServerCreator ? "host" : "client";

        private static string Place()
        {
            string Scene = SceneManager.GetActiveScene().name;
            MMBiomeGeneration.BiomeRoom Room = MMBiomeGeneration.BiomeGenerator.Instance?.CurrentRoom;
            return Room != null && Scene.StartsWith("Dungeon") ? $"{Scene}@{Room.x},{Room.y}" : Scene;
        }

        private static IEnumerator Watch()
        {
            string Last = null;
            float At = 0f;
            while (Count < 40)
            {
                yield return new WaitForSecondsRealtime(1f);
                if (PlayerFarming.Instance == null || MMTransition.IsPlaying || HUD_Manager.Instance == null)
                    continue;
                string Here = Place();
                if (Here == Last && Time.realtimeSinceStartup - At < 30f)
                    continue;
                yield return new WaitForSecondsRealtime(Here == Last ? 0f : 5f);
                if (Place() != Here || MMTransition.IsPlaying)
                    continue;
                Last = Here;
                At = Time.realtimeSinceStartup;
                Take(Here);
            }
        }

        /// <summary>
        /// A frame and the account, now; the name says where
        /// </summary>
        internal static void Take(string Where)
        {
            if (Dir == null)
                return;
            try
            {
                System.IO.Directory.CreateDirectory(Dir);
                string Name = $"{Role}-hud-{++Count:00}-{Where.Replace('@', '_').Replace(',', '_').Replace(' ', '_')}";
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Dir, Name + ".png"));
                File.WriteAllText(System.IO.Path.Combine(Dir, Name + ".txt"), Account());
                Plugin.Logger?.LogWarning($"{Tag} HUD frame {Name}");
            }
            catch (Exception e)
            {
                Plugin.Logger?.LogWarning($"{Tag} HUD frame failed: {e}");
            }
        }

        private static string F(float Value) => Value.ToString("0.#", CultureInfo.InvariantCulture);

        private static string Account()
        {
            StringBuilder Text = new();
            Text.AppendLine($"place {Place()}  session={Plugin.GlobalsInternal.InGameSession} coopActive={CoopManager.CoopActive} players={PlayerFarming.players.Count} " +
                            $"hudHidden={HUD_Manager.Instance?.Hidden} survival={DataManager.Instance?.SurvivalModeActive} difficulty={DifficultyManager.PrimaryDifficulty}");
            Text.AppendLine($"scale: playersCount={PlayerFarming.playersCount} healthContainer={(HUD_Manager.Instance?.healthContainer != null ? F(HUD_Manager.Instance.healthContainer.transform.localScale.x) : "-")}");
            Text.AppendLine($"save: heartsLevel={DataManager.Instance?.PLAYER_HEARTS_LEVEL} healthModified={DataManager.Instance?.PLAYER_HEALTH_MODIFIED} fleece={DataManager.Instance?.PlayerFleece}");

            Text.AppendLine("PLAYERS");
            foreach (PlayerFarming Player in PlayerFarming.players)
            {
                if (Player == null)
                    continue;
                HealthPlayer H = Player.health;
                Text.AppendLine($"  {Player.name} id={Player.playerID} isLamb={Player.isLamb} goat={Player.IsGoat} puppet={COTLMP.Game.PlayerManager.IsPuppet(H)} local={Player == PlayerFarming.Instance} " +
                                $"hp={F(H?.HP ?? -1)}/{F(H?.totalHP ?? -1)} blue={F(H?.BlueHearts ?? 0)} black={F(H?.BlackHearts ?? 0)} spirit={F(H?.SpiritHearts ?? 0)}/{F(H?.TotalSpiritHearts ?? 0)} " +
                                $"fire={F(H?.FireHearts ?? 0)} ice={F(H?.IceHearts ?? 0)} panel={Path(Player.hudHearts?.transform)}");
            }

            Text.AppendLine("HEARTS PANELS");
            foreach (HUD_Hearts Panel in Resources.FindObjectsOfTypeAll<HUD_Hearts>().Where(Each => Each != null && Each.gameObject.scene.IsValid()))
            {
                var Hearts = Panel.HeartIcons.Where(Heart => Heart != null && Heart.gameObject.activeInHierarchy).ToList();
                string Ring = Panel.coopIndicatorIcon == null ? "none" :
                    $"{(Panel.coopIndicatorIcon.gameObject.activeInHierarchy ? "on" : "off")}:{(Panel.coopIndicatorIcon.LambIconContainer != null && Panel.coopIndicatorIcon.LambIconContainer.activeInHierarchy ? "lamb" : "")}" +
                    $"{(Panel.coopIndicatorIcon.GoatIconContainer != null && Panel.coopIndicatorIcon.GoatIconContainer.activeInHierarchy ? "goat" : "")}";
                Text.AppendLine($"  {Path(Panel.transform)} shown={Panel.gameObject.activeInHierarchy} onScreen={OnScreen(Panel.transform as RectTransform)} for={Panel.playerFarming?.name} " +
                                $"isLamb={Panel.isLamb} hearts={Hearts.Count} ring={Ring} faithAmmo={Panel.faithAmmo?.gameObject.activeInHierarchy}");
                CoopIndicatorIcon Icon = Panel.coopIndicatorIcon;
                if (Icon != null && Icon.gameObject.activeInHierarchy)
                {
                    UnityEngine.UI.Image Face = Icon.LambIconContainer != null && Icon.LambIconContainer.activeInHierarchy ? Icon.LambImage : Icon.GoatImage;
                    float Alpha = 1f;
                    foreach (CanvasGroup Group in Icon.GetComponentsInParent<CanvasGroup>())
                        Alpha *= Group.alpha;
                    Text.AppendLine($"    face: {Face?.name} enabled={Face?.enabled} color={Face?.color} scale={F(Icon.transform.localScale.x)}/{F(Icon.transform.lossyScale.x)} " +
                                    $"faceScale={(Face != null ? F(Face.transform.lossyScale.x) : "-")} alpha={F(Alpha)} onScreen={OnScreen(Face?.rectTransform)} pos={Icon.transform.localPosition}");
                }
                if (Panel.faithAmmo != null && Panel.gameObject.activeInHierarchy)
                {
                    foreach (UnityEngine.UI.Image Part in Panel.faithAmmo.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                        Text.AppendLine($"    ring part {Path(Part.transform).Substring(Path(Panel.faithAmmo.transform).Length)} on={Part.isActiveAndEnabled} sprite={Part.sprite?.name} material={Part.material?.name} color={Part.color}");
                }
                if (Hearts.Count > 0)
                {
                    Text.AppendLine("    " + string.Join(" ", Hearts.Select(Heart => $"{Heart.MyHeartType}/{Heart.MyState}/{Heart.Spine?.Skeleton?.Skin?.Name}")));
                    Text.AppendLine($"    sizes: panel {F(Panel.transform.lossyScale.x)}, hearts {string.Join(" ", Hearts.Select(Heart => F(Heart.Spine != null ? Heart.Spine.rectTransform.localScale.x : -1f)))}, " +
                                    $"ring {(Panel.faithAmmo != null ? F(Panel.faithAmmo.transform.lossyScale.x) : "-")}");
                }
            }

            Text.AppendLine("ON SCREEN (HUD)");
            if (HUD_Manager.Instance != null)
                Walk(HUD_Manager.Instance.transform, 0, Text);
            return Text.ToString();
        }

        private static string Path(Transform Item)
        {
            if (Item == null)
                return "-";
            string Name = Item.name;
            for (Transform Up = Item.parent; Up != null && Up != HUD_Manager.Instance?.transform; Up = Up.parent)
                Name = Up.name + "/" + Name;
            return Name;
        }

        /* What is drawn and in sight: every enabled picture or text, under the HUD element it belongs to */
        private static void Walk(Transform Root, int Depth, StringBuilder Text)
        {
            System.Collections.Generic.SortedDictionary<string, int> Seen = new();
            foreach (UnityEngine.UI.Graphic Item in Root.GetComponentsInChildren<UnityEngine.UI.Graphic>(false))
            {
                if (!Item.enabled || Item.color.a < 0.01f || !OnScreen(Item.rectTransform) || Item.GetComponentsInParent<CanvasGroup>().Any(Group => Group.alpha < 0.01f))
                    continue;
                string[] Parts = Path(Item.transform).Split('/');
                string Key = string.Join("/", Parts.Skip(Math.Max(0, Parts.Length - 4)));
                Seen[Key] = Seen.TryGetValue(Key, out int N) ? N + 1 : 1;
            }
            foreach (var Item in Seen)
                Text.AppendLine($"  {Item.Key}{(Item.Value > 1 ? $" x{Item.Value}" : "")}");
        }

        private static readonly Vector3[] Corners = new Vector3[4];

        private static bool OnScreen(RectTransform Rect)
        {
            if (Rect == null)
                return false;
            Canvas Paper = Rect.GetComponentInParent<Canvas>();
            Camera Eye = Paper == null || Paper.renderMode == RenderMode.ScreenSpaceOverlay ? null : Paper.worldCamera;
            Rect.GetWorldCorners(Corners);
            Vector2 Min = new(float.MaxValue, float.MaxValue), Max = new(float.MinValue, float.MinValue);
            foreach (Vector3 Corner in Corners)
            {
                Vector2 Point = RectTransformUtility.WorldToScreenPoint(Eye, Corner);
                Min = Vector2.Min(Min, Point);
                Max = Vector2.Max(Max, Point);
            }
            return Max.x > 0f && Max.y > 0f && Min.x < Screen.width && Min.y < Screen.height;
        }

        /* The title screen (before any press), and where its pictures are */
        private static IEnumerator TitleFrame()
        {
            while (SceneManager.GetActiveScene().name != "Main Menu" || MMTransition.IsPlaying)
                yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForSecondsRealtime(8f);
            Take("title");
            StringBuilder Text = new();
            foreach (UnityEngine.UI.Graphic Item in UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Graphic>())
            {
                if (!Item.isActiveAndEnabled || !OnScreen(Item.rectTransform))
                    continue;
                string Name = Item.name;
                for (Transform Up = Item.transform.parent; Up != null; Up = Up.parent)
                    Name = Up.name + "/" + Name;
                Text.AppendLine($"{Name} [{Item.GetType().Name}] {(Item is UnityEngine.UI.Image Picture ? Picture.sprite?.name : "")} {Item.rectTransform.rect.size}");
            }
            File.WriteAllText(System.IO.Path.Combine(Dir, "title-graphics.txt"), Text.ToString());
        }

        /* The main menu, once it is up past the title */
        private static IEnumerator Menu()
        {
            /* The main menu, or a window over it (the mod's warning, a disconnection) */
            while (Lamb.UI.MainMenu.UIMainMenuController.Instance?.MainMenu == null ||
                   (!Lamb.UI.MainMenu.UIMainMenuController.Instance.MainMenu.isActiveAndEnabled && !Lamb.UI.UIMenuBase.ActiveMenus.Exists(Menu => Menu is src.UI.UIMenuConfirmationWindow)))
                yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForSecondsRealtime(3f);
            Take("menu");
            if (OnlineShot)
            {
                COTLMP.Ui.OnlineMenu.Open();
                yield return new WaitForSecondsRealtime(3f);
                Take("online");
            }
            if (LogTest)
                yield return PressCopyLog();
            if (LangShots)
                yield return Languages();
        }

        /* The window's "Copy the log", pressed: the clipboard must hold the log */
        private static IEnumerator PressCopyLog()
        {
            float Until = Time.realtimeSinceStartup + 20f;
            GameObject Button = null;
            while ((Button = GameObject.Find("COTLMP Copy Log")) == null && Time.realtimeSinceStartup < Until)
                yield return new WaitForSecondsRealtime(0.5f);
            if (Button == null)
            {
                Plugin.Logger?.LogWarning($"{Tag} Log test: no copy button in the window");
                yield break;
            }
            /* The clipboard is the player's: what was on it goes back after */
            string Before = GUIUtility.systemCopyBuffer;
            GUIUtility.systemCopyBuffer = "";
            Button.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(1f);
            string Got = GUIUtility.systemCopyBuffer ?? "";
            GUIUtility.systemCopyBuffer = Before ?? "";
            Plugin.Logger?.LogWarning($"{Tag} Log test: {Got.Length} characters on the clipboard, {(Got.Contains("Chainloader startup complete") ? "the log" : "NOT THE LOG")}");
            Take("log-copied");
        }

        /* Every language the game has, in memory only, with the line box the mod draws itself (IMGUI, the default font) */
        private static IEnumerator Languages()
        {
            string[] All = { "English", "Russian", "Japanese", "French", "German", "Spanish", "Portuguese (Brazil)", "Chinese (Simplified)",
                             "Chinese (Traditional)", "Korean", "Italian", "Dutch", "Turkish", "French (Canadian)", "Arabic" };
            string Was = I2.Loc.LocalizationManager.CurrentLanguage, WasCode = I2.Loc.LocalizationManager.CurrentLanguageCode;
            foreach (string Language in All)
            {
                string Code = I2.Loc.LocalizationManager.GetLanguageCode(Language);
                I2.Loc.LocalizationManager.SetLanguageAndCode(Language, Code, RememberLanguage: false, Force: true);
                yield return new WaitForSecondsRealtime(1.5f);
                string Game = I2.Loc.LocalizationManager.GetTranslation("Conversation_NPC/Fox/Meeting/0", FixForRTL: false);
                if (string.IsNullOrEmpty(Game) || Game.Contains("Conversation_NPC"))
                    Game = I2.Loc.LocalizationManager.GetTranslation("Conversation_NPC/Fox/Answer_CantAfford/0", FixForRTL: false);
                COTLMP.Game.Dialogs.GameLine = Game + "\n" +
                                               string.Format(I2.Loc.LocalizationManager.GetTranslation("Multiplayer/UI/PatchesFailed", FixForRTL: false), 2);
                yield return new WaitForSecondsRealtime(1f);
                Take($"lang-{Code}");
                yield return new WaitForSecondsRealtime(0.5f);
            }
            COTLMP.Game.Dialogs.GameLine = null;
            I2.Loc.LocalizationManager.SetLanguageAndCode(Was, WasCode, RememberLanguage: false, Force: true);
            Plugin.Logger?.LogWarning($"{Tag} Languages shown, back to {Was}");
        }

        /* HOLD TEST **********************************************************/

        private static IEnumerator Hold()
        {
            while (!Plugin.GlobalsInternal.IsServerCreator || SceneManager.GetActiveScene().name != "Dungeon1" || PlayerFarming.Instance == null ||
                   PlayerFarming.players.Count < 2 || MMTransition.IsPlaying)
                yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForSecondsRealtime(3f);

            MMBiomeGeneration.BiomeRoom Room = MMBiomeGeneration.BiomeGenerator.Instance?.CurrentRoom;
            Vector2Int[] Ways = { new(0, 1), new(1, 0), new(0, -1), new(-1, 0) };
            Vector2Int? Way = null;
            foreach (Vector2Int Each in Ways)
            {
                if (Room != null && MMBiomeGeneration.BiomeGenerator.Instance.Rooms.Exists(Other => Other.x == Room.x + Each.x && Other.y == Room.y + Each.y))
                {
                    Way = Each;
                    break;
                }
            }
            if (!Way.HasValue)
            {
                Plugin.Logger?.LogWarning($"{Tag} Hold test: no room next door");
                yield break;
            }
            Vector2Int Go = Way.Value;
            Plugin.Logger?.LogWarning($"{Tag} Hold test: going on alone {Go}");
            MMTransition.Play(MMTransition.TransitionType.ChangeRoom, MMTransition.Effect.BlackFade, MMTransition.NO_SCENE, 0.5f, "", () => MMBiomeGeneration.BiomeGenerator.ChangeRoom(Go));
            yield return new WaitForSecondsRealtime(1f);
            while (MMTransition.IsPlaying)
                yield return null;
            yield return new WaitForSecondsRealtime(2f);

            GameObject Me = PlayerFarming.Instance.gameObject;
            var Entries = new System.Collections.Generic.List<MMTools.ConversationEntry>
            {
                new(Me, "Hold test line one", "talk", "", null, "Lamb"),
                new(Me, "Hold test line two", "talk", "", null, "Lamb")
            };
            string Here = COTLMP.Game.CoopSync.CurrentPlace();
            MMTools.MMConversation.Play(new MMTools.ConversationObject(Entries, null, () => Plugin.Logger?.LogWarning($"{Tag} Hold test: the conversation is over")));
            Plugin.Logger?.LogWarning($"{Tag} Hold test: conversation started in {Here}, held {COTLMP.Game.SharedScenes.HoldingRoom}, state {PlayerFarming.Instance.state.CURRENT_STATE}, partner in {COTLMP.Game.RoomSync.PartnerPlace}");
            yield return new WaitForSecondsRealtime(1f);
            Take("hold-waiting");

            Door Way2 = Door.Doors.Find(Item => Item != null && Item.isActiveAndEnabled);
            if (Way2 != null && COTLMP.Game.SharedScenes.HoldingRoom)
            {
                typeof(Door).GetMethod("OnTriggerEnter2D", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                    ?.Invoke(Way2, new object[] { PlayerFarming.Instance.circleCollider2D });
                yield return new WaitForSecondsRealtime(2f);
                Plugin.Logger?.LogWarning($"{Tag} Hold test: tried a door, still in {COTLMP.Game.CoopSync.CurrentPlace()} (was {Here}), transition {MMTransition.IsPlaying}");
            }

            float Until = Time.realtimeSinceStartup + 60f;
            while (COTLMP.Game.SharedScenes.HoldingRoom && Time.realtimeSinceStartup < Until)
                yield return null;
            Plugin.Logger?.LogWarning($"{Tag} Hold test: held {COTLMP.Game.SharedScenes.HoldingRoom}, conversation on {MMTools.MMConversation.isPlaying}, partner in {COTLMP.Game.RoomSync.PartnerPlace}");
            yield return new WaitForSecondsRealtime(1.5f);
            Take("hold-started");
        }

        /* GEAR TEST **********************************************************/

        private static bool Geared;

        private static IEnumerator Gear()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(1f);
                if (!COTLMP.Game.CoopSync.InSession || PlayerFarming.Instance == null || MMTransition.IsPlaying)
                    continue;
                if (!TestHarness.OnTestSave("-cotlmp-geartest"))
                    yield break;

                /* The host saves a while after the friend's character came, so it is written with the save */
                if (Plugin.GlobalsInternal.IsServerCreator)
                {
                    if (COTLMP.Game.Rejoin.CharactersTaken > 0 && !Geared && !GameManager.IsDungeon(PlayerFarming.Location) || COTLMP.Game.Rejoin.CharactersTaken > 12 && !Geared)
                    {
                        Geared = true;
                        yield return new WaitForSecondsRealtime(20f);
                        Plugin.Logger?.LogWarning($"{Tag} Gear test: the host saves its game");
                        SaveAndLoad.Save();
                    }
                    continue;
                }

                if (Geared || COTLMP.Game.Rejoin.Going || !GameManager.IsDungeon(PlayerFarming.Location))
                    continue;
                yield return new WaitForSecondsRealtime(8f);
                PlayerFarming Player = PlayerFarming.Instance;
                if (Player == null || COTLMP.Game.Rejoin.Going)
                    continue;
                Geared = true;
                Player.playerWeapon.SetWeapon(EquipmentType.Axe, 2);
                Player.playerSpells.SetSpell(EquipmentType.Tentacles, 1);
                TrinketManager.AddTrinket(new TarotCards.TarotCard(TarotCards.Card.Moon, 0), Player);
                Player.health.DealDamage(1f, Player.gameObject, Player.transform.position, false, Health.AttackTypes.Melee, true);
                DataManager.Instance.ChoreXPLevel = 3;
                DataManager.Instance.ChoreXP = 7f;
                yield return new WaitForSecondsRealtime(1f);
                Plugin.Logger?.LogWarning($"{Tag} Gear test: now {Player.currentWeapon} {Player.currentWeaponLevel}, {Player.currentCurse} {Player.currentCurseLevel}, " +
                                          $"cards {string.Join(",", Player.RunTrinkets.Select(Card => Card.CardType))}, hearts {Player.health.HP}/{Player.health.totalHP}, chores {DataManager.Instance.ChoreXPLevel}");
            }
        }

        /* THE GAME'S OWN CO-OP ***********************************************/

        private static IEnumerator PlayGameCoop()
        {
            while (SceneManager.GetActiveScene().name != "Base Biome 1" || PlayerFarming.Instance == null || MMTransition.IsPlaying || CoopManager.Instance == null)
                yield return null;
            yield return new WaitForSecondsRealtime(8f);
            float Until = Time.realtimeSinceStartup + 30f;
            while (PlayerFarming.Instance.state.CURRENT_STATE == StateMachine.State.InActive && Time.realtimeSinceStartup < Until)
                yield return null;
            if (PlayerFarming.Instance.state.CURRENT_STATE == StateMachine.State.InActive)
                PlayerFarming.Instance.state.CURRENT_STATE = StateMachine.State.Idle;
            HUD_Manager.Instance?.Show(0, true);
            yield return new WaitForSecondsRealtime(3f);
            Take("Base-solo");
            Plugin.Logger?.LogWarning($"{Tag} The game's own second player joins");
            /* Without a pad of its own the game would take the second player out at once */
            CoopManager.Instance.temporaryDisableRemoval = true;
            CoopManager.Instance.SpawnCoopPlayer(1);
            yield return new WaitForSecondsRealtime(12f);
            Plugin.Logger?.LogWarning($"{Tag} Players now {PlayerFarming.players.Count}, co-op {CoopManager.CoopActive}; waits on goToAndStop={PlayerFarming.Instance.GoToAndStopping} " +
                                      $"state={PlayerFarming.Instance.state.CURRENT_STATE} letterbox={LetterBox.IsPlaying} talk={MMConversation.isPlaying}");
            HUD_Manager.Instance?.Show(0, true);
            yield return new WaitForSecondsRealtime(3f);
            Take("Base-coop");
            yield return new WaitForSecondsRealtime(5f);

            Plugin.Logger?.LogWarning($"{Tag} Into the first dungeon together, as the base door does");
            GameManager.NewRun("", false, FollowerLocation.Dungeon1_1);
            MMTransition.Play(MMTransition.TransitionType.ChangeRoomWaitToResume, MMTransition.Effect.BlackFade, "Dungeon1", 1f, "", null);
            while (SceneManager.GetActiveScene().name != "Dungeon1" || PlayerFarming.Instance == null)
                yield return null;
            yield return new WaitForSecondsRealtime(3f);
            MMTransition.CanResume = true;
            MMTransition.ResumePlay();
            yield return new WaitForSecondsRealtime(4f);
            if (PlayerFarming.players.Count < 2)
            {
                Plugin.Logger?.LogWarning($"{Tag} The second player is gone on the way: joins again in the dungeon");
                CoopManager.Instance.temporaryDisableRemoval = true;
                CoopManager.Instance.SpawnCoopPlayer(1, false);
                float Until2 = Time.realtimeSinceStartup + 30f;
                while (PlayerFarming.players.Count < 2 && Time.realtimeSinceStartup < Until2)
                {
                    /* The game waits for the first player to stand still and be free */
                    if (PlayerFarming.Instance.state.CURRENT_STATE == StateMachine.State.InActive)
                        PlayerFarming.Instance.state.CURRENT_STATE = StateMachine.State.Idle;
                    yield return null;
                }
                Plugin.Logger?.LogWarning($"{Tag} Players now {PlayerFarming.players.Count}, co-op {CoopManager.CoopActive}");
            }
            yield return new WaitForSecondsRealtime(8f);
            HUD_Manager.Instance?.Show(0, true);
            yield return new WaitForSecondsRealtime(3f);
            Take("Dungeon1-coop");

            /* A hit on each, to see the hearts go down */
            foreach (PlayerFarming Player in PlayerFarming.players.ToList())
                Player?.health?.DealDamage(1f, Player.gameObject, Player.transform.position, false, Health.AttackTypes.Melee, true);
            yield return new WaitForSecondsRealtime(3f);
            Take("Dungeon1-hit");
        }
    }
}

/* EOF */
