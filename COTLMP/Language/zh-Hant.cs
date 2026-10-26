/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Chinese (Traditional) localization file
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP;
using COTLMP.Api;
using COTLMP.Data;

/* TRANSLATION ****************************************************************/

namespace COTLMP.Language
{
    public class ChineseTraditional
    {
        public static LocalizationTable[] StringsTable =
        [
            new("UI/DLC", "多人遊戲", true),
            new("UI/Banner", $"多人遊戲版 v{COTLMP.Data.Version.CotlMpVer}", false),
            new("Multiplayer/UI/TitleDialog", "多人遊戲", false),
            new("Multiplayer/UI/Settings", "多人遊戲設定", false),
            new("Multiplayer/UI/Settings/DisableMod", "啟用/停用模組", false),
            new("Multiplayer/UI/Settings/PlayerName", "玩家名稱", false),
            new("Multiplayer/UI/Settings/PlayerCount", "玩家人數上限", false),
            new("Multiplayer/UI/StartServer", "開放線上", false),
            new("Multiplayer/UI/SteamInviteHint", "Steam 好友可透過邀請（Shift+Tab）或「加入遊戲」加入", false),
            new("Multiplayer/UI/ServerStarted", "停止主持", false),
            new("Multiplayer/UI/ServerConfirm", "停止主持？你的好友會中斷連線，你會繼續自己的遊戲。", false),
            new("Multiplayer/UI/LeaveServer", "離開伺服器", false),
            new("Multiplayer/UI/LeaveConfirm", "要離開伺服器嗎？", false),
            new("Multiplayer/UI/Disconnected", "已中斷連線", false),
            new("Multiplayer/UI/DisconnectedError", "發生錯誤（請查看主控台）", false),
            new("Multiplayer/UI/Players", "玩家", false),
            new("Multiplayer/UI/Host", "主機", false),
            new("Multiplayer/UI/You", "你", false),
            new("Multiplayer/UI/Ms", "毫秒", false),
            new("Multiplayer/Steam/AfterPrologue", "線上遊戲會在序章結束後於你的教團中開放", false),
            new("Multiplayer/Steam/LoadingHostSave", "正在載入好友的遊戲", false),
            new("Multiplayer/Steam/HostSaveFailed", "無法載入好友的遊戲", false),
            new("Multiplayer/Steam/Joined", "已加入好友的遊戲", false),
            new("Multiplayer/Steam/JoinFailed", "無法連線到好友", false),
            new("Multiplayer/Steam/HostLeft", "好友已停止主持", false),
            new("Multiplayer/Reconnect/Connecting", "正在連線 {0}/{1}", false),
            new("Multiplayer/Reconnect/Failed", "與好友的連線已中斷：嘗試 3 次仍無法重新連線", false),
            new("Multiplayer/UI/PatchesFailedTitle", "Co-op Anything", false),
            new("Multiplayer/UI/PatchesFailed", "模組有 {0} 個掛鉤與此遊戲版本不符：遊戲在模組之後更新了。在修正版本推出前，合作模式可能無法正常運作；修正版一推出，模組就會在這裡提示更新", false),
            new("Multiplayer/UI/PatchesUpdate", "模組有 {0} 個掛鉤與此遊戲版本不符：遊戲在模組之後更新了。已推出版本 {1}（目前 {2}）。現在更新嗎？", false),
            new("Multiplayer/UI/PatchesOurs", "模組有 {0} 個掛鉤與你的遊戲版本不符：合作模式可能無法正常運作", false),
            new("Multiplayer/UI/PatchesPartner", "{0}：模組有 {1} 個掛鉤與其遊戲版本不符，合作模式可能無法正常運作", false),
            new("Multiplayer/Chat/Tip", "按 T 與好友聊天", false),
            new("Multiplayer/Chat/Hint", "訊息… Enter 傳送，Esc 關閉", false),
            new("Multiplayer/Steam/Left", "你離開了好友的遊戲", false),
            new("Multiplayer/Steam/Stopped", "已停止主持", false),
            new("Multiplayer/Steam/LeaveFirst", "停止主持後才能加入好友", false),
            new("Multiplayer/Cult/FollowerBusy", "已經有人在和這名信徒交談", false),
            new("Multiplayer/Scene/WaitPartner", "正在等待同伴進入", false),
            new("Multiplayer/Scene/WaitScene", "正在等待同伴看完", false),
            new("Multiplayer/Scene/WaitSync", "正在等待同步確認", false),
            new("Multiplayer/Version/Title", "模組版本不同", false),
            new("Multiplayer/Version/Lines", "主機版本：{0}\n你的版本：{1}", false),
            new("Multiplayer/Version/FriendLines", "好友版本：{0}\n你的版本：{1}", false),
            new("Multiplayer/Version/Unknown", "未知（0.2.8 或更舊）", false),
            new("Multiplayer/Version/Risk", "你可以繼續遊戲，但版本不同可能導致錯誤，甚至損壞存檔。", false),
            new("Multiplayer/Version/UpdateToHost", "更新到 {0}？更新會直接在主選單中完成，之後自動重新加入主機。", false),
            new("Multiplayer/Version/HostOlder", "主機的版本較舊。你可以請主機更新。", false),
            new("Multiplayer/Version/HostUpdate", "現在更新？會在你遊戲時下載；之後由你選擇如何安裝：用此存檔回到大廳，或重新啟動遊戲。", false),
            new("Multiplayer/Version/FriendAsks", "好友請你更新模組", false),
            new("Multiplayer/Version/FriendHas", "好友的模組為 {0}，你的為 {1}", false),
            new("Multiplayer/Version/Update", "更新", false),
            new("Multiplayer/Version/PlayAnyway", "仍然遊戲", false),
            new("Multiplayer/Version/Suggest", "請主機更新", false),
            new("Multiplayer/Version/NotNow", "以後再說", false),
            new("Multiplayer/Online/Title", "線上遊戲", false),
            new("Multiplayer/Online/CreateLobby", "建立大廳", false),
            new("Multiplayer/Online/FriendsLobbies", "好友的大廳", false),
            new("Multiplayer/Online/NoLobbies", "沒有好友開啟大廳", false),
            new("Multiplayer/Online/Joining", "正在加入 {0}...", false),
            new("Multiplayer/Online/PickSave", "大廳使用的存檔", false),
            new("Multiplayer/Online/Back", "返回", false),
            new("Multiplayer/Lobby/Title", "大廳", false),
            new("Multiplayer/Lobby/NewGame", "新遊戲", false),
            new("Multiplayer/Lobby/HostSave", "{0} 的遊戲：{1}", false),
            new("Multiplayer/Lobby/Play", "開始", false),
            new("Multiplayer/Lobby/Invite", "邀請好友", false),
            new("Multiplayer/Lobby/Leave", "離開大廳", false),
            new("Multiplayer/Lobby/Close", "關閉大廳", false),
            new("Multiplayer/Lobby/WaitingFriend", "正在等待好友...", false),
            new("Multiplayer/Lobby/WaitingHost", "正在等待主機開始", false),
            new("Multiplayer/Lobby/Failed", "無法開啟大廳", false),
            new("Multiplayer/Intro/WaitCrown", "正在等待好友獲得王冠...", false),
            new("Multiplayer/UI/OnlineOpen", "線上遊戲已開放", false),
            new("Multiplayer/UI/OnlineFailed", "無法開放線上遊戲", false),
            new("Multiplayer/Steam/GameVersion", "好友的遊戲版本為 {0}，你的為 {1}。請雙方都在 Steam 中更新遊戲。", false),
            new("Multiplayer/Fox/HostDeals", "與狐狸的交易由主機進行", false),
            new("Multiplayer/Update/Title", "合作模組的新版本", false),
            new("Multiplayer/Update/Body", "版本 {0} 已推出，你的是 {1}。現在更新？無需離開遊戲，就在這裡完成。只有需要新版 BepInEx 或 COTL API 的版本才會為安裝程式重新啟動遊戲。", false),
            new("Multiplayer/Update/Downloading", "正在下載更新…", false),
            new("Multiplayer/Update/Screen", "正在將合作模組更新到 {0}", false),
            new("Multiplayer/Update/Progress", "已下載：{0} / {1} KB", false),
            new("Multiplayer/Update/Checking", "正在驗證下載", false),
            new("Multiplayer/Update/Starting", "正在啟動新版本", false),
            new("Multiplayer/Update/Restart", "此版本需要安裝程式：遊戲將重新啟動", false),
            new("Multiplayer/Update/Ready", "更新已就緒：將在主選單中啟用", false),
            new("Multiplayer/Update/Done", "合作模組已更新", false),
            new("Multiplayer/Update/ReadyTitle", "更新已就緒", false),
            new("Multiplayer/Update/ReadyHost", "版本 {0} 已下載。安裝時會儲存遊戲，你會回到主選單，並用此存檔重新開啟大廳；好友照常加入。在你按下「開始」之前不會進入遊戲。", false),
            new("Multiplayer/Update/ReadyFriend", "版本 {0} 已下載。安裝時你會回到主選單並重新加入主機。", false),
            new("Multiplayer/Update/RestartLobby", "重新開啟大廳", false),
            new("Multiplayer/Update/OtherWays", "其他方式", false),
            new("Multiplayer/Update/RestartGameBody", "或者儲存遊戲並重新啟動，然後開啟同一個大廳。稍後：下次你回到主選單或啟動遊戲時安裝更新。", false),
            new("Multiplayer/Update/RestartGame", "重新啟動遊戲", false),
            new("Multiplayer/Update/Later", "稍後", false),
            new("Multiplayer/Update/Saving", "正在儲存遊戲...", false),
            new("Multiplayer/Rejoin/Going", "正在回到主機身邊...", false),
            new("Multiplayer/Scene/GameSpeaker", "遊戲", false),
            new("Multiplayer/Scene/HoldTalk", "正在等待同伴：同伴進入這個房間後對話就會開始。在此期間你可以在房間內走動，房門會在對話結束後打開。", false),
            new("Multiplayer/Scene/PartnerWaits", "同伴正在等你：你到達後對話就會開始", false),
            new("Multiplayer/Update/Failed", "無法自動更新", false),
            new("Multiplayer/Update/OpenPage", "開啟版本 {0} 的頁面進行下載？", false)
        ];
    }
}

/* EOF */
