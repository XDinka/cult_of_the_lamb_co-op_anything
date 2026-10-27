/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Chinese (Simplified) localization file
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP;
using COTLMP.Api;
using COTLMP.Data;

/* TRANSLATION ****************************************************************/

namespace COTLMP.Language
{
    public class ChineseSimplified
    {
        public static LocalizationTable[] StringsTable =
        [
            new("UI/DLC", "多人游戏", true),
            new("UI/Banner", $"多人游戏版 v{COTLMP.Data.Version.CotlMpVer}", false),
            new("Multiplayer/UI/TitleDialog", "多人游戏", false),
            new("Multiplayer/UI/Settings", "多人游戏设置", false),
            new("Multiplayer/UI/Settings/DisableMod", "启用/禁用模组", false),
            new("Multiplayer/UI/Settings/PlayerName", "玩家名称", false),
            new("Multiplayer/UI/StartServer", "开放在线", false),
            new("Multiplayer/UI/SteamInviteHint", "Steam 好友可通过邀请（Shift+Tab）或“加入游戏”加入", false),
            new("Multiplayer/UI/ServerStarted", "停止主持", false),
            new("Multiplayer/UI/ServerConfirm", "停止主持？你的好友会断开连接，你会继续自己的游戏。", false),
            new("Multiplayer/UI/LeaveServer", "离开服务器", false),
            new("Multiplayer/UI/LeaveConfirm", "要离开服务器吗？", false),
            new("Multiplayer/UI/Disconnected", "已断开连接", false),
            new("Multiplayer/UI/DisconnectedError", "发生错误（请查看控制台）", false),
            new("Multiplayer/UI/Players", "玩家", false),
            new("Multiplayer/UI/Host", "主机", false),
            new("Multiplayer/UI/You", "你", false),
            new("Multiplayer/UI/Ms", "毫秒", false),
            new("Multiplayer/Steam/AfterPrologue", "在线游戏会在序章结束后于你的教团中开放", false),
            new("Multiplayer/Steam/LoadingHostSave", "正在加载好友的游戏", false),
            new("Multiplayer/Steam/HostSaveFailed", "无法加载好友的游戏", false),
            new("Multiplayer/Steam/Joined", "已加入好友的游戏", false),
            new("Multiplayer/Steam/JoinFailed", "无法连接到好友", false),
            new("Multiplayer/Steam/HostLeft", "好友已停止主持", false),
            new("Multiplayer/Reconnect/Connecting", "正在连接 {0}/{1}", false),
            new("Multiplayer/Reconnect/Failed", "与好友的连接已断开：3 次尝试均未能重新连接", false),
            new("Multiplayer/UI/PatchesFailedTitle", "Co-op Anything", false),
            new("Multiplayer/UI/PatchesFailed", "模组有 {0} 个挂钩与此游戏版本不符：游戏在模组之后更新了。在修复版本发布前，合作模式可能无法正常运行；修复版一发布，模组就会在这里提示更新", false),
            new("Multiplayer/UI/PatchesUpdate", "模组有 {0} 个挂钩与此游戏版本不符：游戏在模组之后更新了。已发布版本 {1}（当前 {2}）。现在更新吗？", false),
            new("Multiplayer/UI/PatchesOurs", "模组有 {0} 个挂钩与你的游戏版本不符：合作模式可能无法正常运行", false),
            new("Multiplayer/UI/PatchesPartner", "{0}：模组有 {1} 个挂钩与其游戏版本不符，合作模式可能无法正常运行", false),
            new("Multiplayer/UI/CopyLog", "复制日志", false),
            new("Multiplayer/UI/LogCopied", "已复制", false),
            new("Multiplayer/Online/Discord", "问题与建议", false),
            new("Multiplayer/Chat/Tip", "按 T 与好友聊天", false),
            new("Multiplayer/Chat/Hint", "消息… Enter 发送，Esc 关闭", false),
            new("Multiplayer/Steam/Left", "你离开了好友的游戏", false),
            new("Multiplayer/Steam/Stopped", "已停止主持", false),
            new("Multiplayer/Steam/LeaveFirst", "停止主持后才能加入好友", false),
            new("Multiplayer/Cult/FollowerBusy", "已经有人在和这名信徒交谈", false),
            new("Multiplayer/Scene/WaitPartner", "正在等待同伴进入", false),
            new("Multiplayer/Scene/WaitScene", "正在等待同伴看完", false),
            new("Multiplayer/Scene/WaitSync", "正在等待同步确认", false),
            new("Multiplayer/Version/Title", "模组版本不同", false),
            new("Multiplayer/Version/Lines", "主机版本：{0}\n你的版本：{1}", false),
            new("Multiplayer/Version/FriendLines", "好友版本：{0}\n你的版本：{1}", false),
            new("Multiplayer/Version/Unknown", "未知（0.2.8 或更早）", false),
            new("Multiplayer/Version/Risk", "你可以继续游戏，但版本不同可能导致错误，甚至损坏存档。", false),
            new("Multiplayer/Version/UpdateToHost", "更新到 {0}？更新会直接在主菜单中完成，之后自动重新加入主机。", false),
            new("Multiplayer/Version/HostOlder", "主机的版本较旧。你可以请主机更新。", false),
            new("Multiplayer/Version/HostUpdate", "现在更新？会在你游戏时下载；之后由你选择如何安装：用此存档返回大厅，或重启游戏。", false),
            new("Multiplayer/Version/FriendAsks", "好友请你更新模组", false),
            new("Multiplayer/Version/FriendHas", "好友的模组为 {0}，你的为 {1}", false),
            new("Multiplayer/Version/Update", "更新", false),
            new("Multiplayer/Version/PlayAnyway", "仍然游戏", false),
            new("Multiplayer/Version/Suggest", "请主机更新", false),
            new("Multiplayer/Version/NotNow", "以后再说", false),
            new("Multiplayer/Online/Title", "在线游戏", false),
            new("Multiplayer/Online/CreateLobby", "创建大厅", false),
            new("Multiplayer/Online/FriendsLobbies", "好友的大厅", false),
            new("Multiplayer/Online/NoLobbies", "没有好友开启大厅", false),
            new("Multiplayer/Online/Joining", "正在加入 {0}...", false),
            new("Multiplayer/Online/PickSave", "大厅使用的存档", false),
            new("Multiplayer/Online/Back", "返回", false),
            new("Multiplayer/Lobby/Title", "大厅", false),
            new("Multiplayer/Lobby/NewGame", "新游戏", false),
            new("Multiplayer/Lobby/HostSave", "{0} 的游戏：{1}", false),
            new("Multiplayer/Lobby/Play", "开始", false),
            new("Multiplayer/Lobby/Invite", "邀请好友", false),
            new("Multiplayer/Lobby/Leave", "离开大厅", false),
            new("Multiplayer/Lobby/Close", "关闭大厅", false),
            new("Multiplayer/Lobby/WaitingFriend", "正在等待好友...", false),
            new("Multiplayer/Lobby/WaitingHost", "正在等待主机开始", false),
            new("Multiplayer/Lobby/Failed", "无法开启大厅", false),
            new("Multiplayer/Intro/WaitCrown", "正在等待好友获得王冠...", false),
            new("Multiplayer/UI/OnlineOpen", "在线游戏已开放", false),
            new("Multiplayer/UI/OnlineFailed", "无法开放在线游戏", false),
            new("Multiplayer/Steam/GameVersion", "好友的游戏版本为 {0}，你的为 {1}。请双方都在 Steam 中更新游戏。", false),
            new("Multiplayer/Fox/HostDeals", "与狐狸的交易由主机进行", false),
            new("Multiplayer/Update/Title", "合作模组的新版本", false),
            new("Multiplayer/Update/Body", "版本 {0} 已发布，你的是 {1}。现在更新？无需退出游戏，就在这里完成。只有需要新版 BepInEx 或 COTL API 的版本才会为安装程序重启游戏。", false),
            new("Multiplayer/Update/Downloading", "正在下载更新…", false),
            new("Multiplayer/Update/Screen", "正在将合作模组更新到 {0}", false),
            new("Multiplayer/Update/Progress", "已下载：{0} / {1} KB", false),
            new("Multiplayer/Update/Checking", "正在校验下载", false),
            new("Multiplayer/Update/Starting", "正在启动新版本", false),
            new("Multiplayer/Update/Restart", "此版本需要安装程序：游戏将重新启动", false),
            new("Multiplayer/Update/Ready", "更新已就绪：将在主菜单中启用", false),
            new("Multiplayer/Update/Done", "合作模组已更新", false),
            new("Multiplayer/Update/ReadyTitle", "更新已就绪", false),
            new("Multiplayer/Update/ReadyHost", "版本 {0} 已下载。安装时会保存游戏，你会回到主菜单，并用此存档重新打开大厅；好友照常加入。在你按下“开始”之前不会进入游戏。", false),
            new("Multiplayer/Update/ReadyFriend", "版本 {0} 已下载。安装时你会回到主菜单并重新加入主机。", false),
            new("Multiplayer/Update/RestartLobby", "重启大厅", false),
            new("Multiplayer/Update/OtherWays", "其他方式", false),
            new("Multiplayer/Update/RestartGameBody", "或者保存游戏并重启，然后打开同一个大厅。稍后：下次你回到主菜单或启动游戏时安装更新。", false),
            new("Multiplayer/Update/RestartGame", "重启游戏", false),
            new("Multiplayer/Update/Later", "稍后", false),
            new("Multiplayer/Update/Saving", "正在保存游戏...", false),
            new("Multiplayer/Rejoin/Going", "正在返回主机身边...", false),
            new("Multiplayer/Scene/GameSpeaker", "游戏", false),
            new("Multiplayer/Scene/HoldTalk", "正在等待同伴：同伴进入这个房间后对话就会开始。在此期间你可以在房间内走动，房门会在对话结束后打开。", false),
            new("Multiplayer/Scene/PartnerWaits", "同伴正在等你：你到达后对话就会开始", false),
            new("Multiplayer/Update/Failed", "无法自动更新", false),
            new("Multiplayer/Update/OpenPage", "打开版本 {0} 的页面进行下载？", false)
        ];
    }
}

/* EOF */
