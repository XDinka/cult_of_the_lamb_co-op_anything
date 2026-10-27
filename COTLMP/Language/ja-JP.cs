/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Japanese localization file
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP;
using COTLMP.Api;
using COTLMP.Data;

/* TRANSLATION ****************************************************************/

namespace COTLMP.Language
{
    public class Japanese
    {
        public static LocalizationTable[] StringsTable =
        [
            new("UI/DLC", "マルチプレイ", true),
            new("UI/Banner", $"マルチプレイ版 v{COTLMP.Data.Version.CotlMpVer}", false),
            new("Multiplayer/UI/TitleDialog", "マルチプレイ", false),
            new("Multiplayer/UI/Settings", "マルチプレイ設定", false),
            new("Multiplayer/UI/Settings/DisableMod", "MODの有効/無効", false),
            new("Multiplayer/UI/Settings/PlayerName", "プレイヤー名", false),
            new("Multiplayer/UI/StartServer", "オンラインを開く", false),
            new("Multiplayer/UI/SteamInviteHint", "Steamのフレンドは招待（Shift+Tab）か「ゲームに参加」から参加できます", false),
            new("Multiplayer/UI/ServerStarted", "ホストを終了", false),
            new("Multiplayer/UI/ServerConfirm", "ホストを終了しますか？フレンドは切断され、あなたは自分のゲームを続けます。", false),
            new("Multiplayer/UI/LeaveServer", "サーバーから退出", false),
            new("Multiplayer/UI/LeaveConfirm", "サーバーから退出しますか？", false),
            new("Multiplayer/UI/Disconnected", "切断されました", false),
            new("Multiplayer/UI/DisconnectedError", "エラーが発生しました（コンソールを確認してください）", false),
            new("Multiplayer/UI/Players", "プレイヤー", false),
            new("Multiplayer/UI/Host", "ホスト", false),
            new("Multiplayer/UI/You", "あなた", false),
            new("Multiplayer/UI/Ms", "ms", false),
            new("Multiplayer/Steam/AfterPrologue", "オンラインプレイはプロローグの後、あなたの教団で始められます", false),
            new("Multiplayer/Steam/LoadingHostSave", "フレンドのゲームを読み込み中", false),
            new("Multiplayer/Steam/HostSaveFailed", "フレンドのゲームを読み込めませんでした", false),
            new("Multiplayer/Steam/Joined", "フレンドに参加しました", false),
            new("Multiplayer/Steam/JoinFailed", "フレンドに接続できませんでした", false),
            new("Multiplayer/Steam/HostLeft", "フレンドがホストを終了しました", false),
            new("Multiplayer/Reconnect/Connecting", "接続中 {0}/{1}", false),
            new("Multiplayer/Reconnect/Failed", "フレンドとの接続が切れました。3回試しましたが再接続できませんでした", false),
            new("Multiplayer/UI/PatchesFailedTitle", "Co-op Anything", false),
            new("Multiplayer/UI/PatchesFailed", "MODのフック {0} 個がこのゲームバージョンに合いません。ゲームがMODより後に更新されました。修正版が出るまで協力プレイが正しく動かない可能性があります。修正版が出たらここで更新を案内します", false),
            new("Multiplayer/UI/PatchesUpdate", "MODのフック {0} 個がこのゲームバージョンに合いません。ゲームがMODより後に更新されました。バージョン {1} が出ています（現在 {2}）。今すぐ更新しますか？", false),
            new("Multiplayer/UI/PatchesOurs", "MODのフック {0} 個がこのゲームバージョンに合いません。協力プレイが正しく動かない可能性があります", false),
            new("Multiplayer/UI/PatchesPartner", "{0}：MODのフック {1} 個が相手のゲームバージョンに合いません。協力プレイが正しく動かない可能性があります", false),
            new("Multiplayer/UI/CopyLog", "ログをコピー", false),
            new("Multiplayer/UI/LogCopied", "コピーしました", false),
            new("Multiplayer/Online/Discord", "不具合と提案", false),
            new("Multiplayer/Chat/Tip", "Tキーでフレンドとチャット", false),
            new("Multiplayer/Chat/Hint", "メッセージ… Enterで送信、Escで閉じる", false),
            new("Multiplayer/Steam/Left", "フレンドのゲームから退出しました", false),
            new("Multiplayer/Steam/Stopped", "ホストを終了しました", false),
            new("Multiplayer/Steam/LeaveFirst", "フレンドに参加するにはホストを終了してください", false),
            new("Multiplayer/Cult/FollowerBusy", "この信者には既に誰かが話しかけています", false),
            new("Multiplayer/Scene/WaitPartner", "パートナーが入ってくるのを待っています", false),
            new("Multiplayer/Scene/WaitScene", "パートナーが見終わるのを待っています", false),
            new("Multiplayer/Scene/WaitSync", "同期の確認を待っています", false),
            new("Multiplayer/Version/Title", "MODのバージョンが異なります", false),
            new("Multiplayer/Version/Lines", "ホストのバージョン: {0}\nあなたのバージョン: {1}", false),
            new("Multiplayer/Version/FriendLines", "フレンドのバージョン: {0}\nあなたのバージョン: {1}", false),
            new("Multiplayer/Version/Unknown", "不明（0.2.8以前）", false),
            new("Multiplayer/Version/Risk", "プレイはできますが、バージョンが異なるとエラーが起きたり、セーブデータが壊れたりする恐れがあります。", false),
            new("Multiplayer/Version/UpdateToHost", "{0} に更新しますか？更新はこのままメインメニューで適用され、その後自動的にホストへ再参加します。", false),
            new("Multiplayer/Version/HostOlder", "ホストのバージョンの方が古いです。ホストに更新を頼むことができます。", false),
            new("Multiplayer/Version/HostUpdate", "今すぐ更新しますか？プレイ中にダウンロードされ、その後に適用方法を選べます：このセーブでロビーに戻るか、ゲームを再起動するか。", false),
            new("Multiplayer/Version/FriendAsks", "フレンドがMODの更新を求めています", false),
            new("Multiplayer/Version/FriendHas", "フレンドのMODは {0}、あなたのは {1} です", false),
            new("Multiplayer/Version/Update", "更新する", false),
            new("Multiplayer/Version/PlayAnyway", "このままプレイ", false),
            new("Multiplayer/Version/Suggest", "ホストに更新を頼む", false),
            new("Multiplayer/Version/NotNow", "今はしない", false),
            new("Multiplayer/Online/Title", "オンラインプレイ", false),
            new("Multiplayer/Online/CreateLobby", "ロビーを作成", false),
            new("Multiplayer/Online/FriendsLobbies", "フレンドのロビー", false),
            new("Multiplayer/Online/NoLobbies", "ロビーを開いているフレンドはいません", false),
            new("Multiplayer/Online/Joining", "{0} に参加中...", false),
            new("Multiplayer/Online/PickSave", "ロビーで使うセーブ", false),
            new("Multiplayer/Online/Back", "戻る", false),
            new("Multiplayer/Lobby/Title", "ロビー", false),
            new("Multiplayer/Lobby/NewGame", "ニューゲーム", false),
            new("Multiplayer/Lobby/HostSave", "{0} のゲーム: {1}", false),
            new("Multiplayer/Lobby/Play", "プレイ", false),
            new("Multiplayer/Lobby/Invite", "フレンドを招待", false),
            new("Multiplayer/Lobby/Leave", "ロビーを出る", false),
            new("Multiplayer/Lobby/Close", "ロビーを閉じる", false),
            new("Multiplayer/Lobby/WaitingFriend", "フレンドを待っています...", false),
            new("Multiplayer/Lobby/WaitingHost", "ホストの開始を待っています", false),
            new("Multiplayer/Lobby/Failed", "ロビーを開けませんでした", false),
            new("Multiplayer/Intro/WaitCrown", "フレンドが王冠を受け取るのを待っています...", false),
            new("Multiplayer/UI/OnlineOpen", "オンラインプレイを開きました", false),
            new("Multiplayer/UI/OnlineFailed", "オンラインプレイを開けませんでした", false),
            new("Multiplayer/Steam/GameVersion", "フレンドのゲームはバージョン {0}、あなたのは {1} です。お互いにSteamでゲームを更新してください。", false),
            new("Multiplayer/Fox/HostDeals", "キツネとの取引はホストが行います", false),
            new("Multiplayer/Update/Title", "協力プレイMODの新しいバージョン", false),
            new("Multiplayer/Update/Body", "バージョン {0} が公開されました（現在は {1}）。今すぐ更新しますか？ゲームを終了せずにこの場で適用されます。新しいBepInExやCOTL APIが必要なバージョンだけは、インストーラーのためにゲームを再起動します。", false),
            new("Multiplayer/Update/Downloading", "更新をダウンロード中…", false),
            new("Multiplayer/Update/Screen", "協力プレイMODを {0} に更新中", false),
            new("Multiplayer/Update/Progress", "ダウンロード済み: {0} / {1} KB", false),
            new("Multiplayer/Update/Checking", "ダウンロードを確認中", false),
            new("Multiplayer/Update/Starting", "新しいバージョンを起動中", false),
            new("Multiplayer/Update/Restart", "このバージョンにはインストーラーが必要です：ゲームを再起動します", false),
            new("Multiplayer/Update/Ready", "更新の準備ができました：メインメニューで適用されます", false),
            new("Multiplayer/Update/Done", "協力プレイMODを更新しました", false),
            new("Multiplayer/Update/ReadyTitle", "更新の準備ができました", false),
            new("Multiplayer/Update/ReadyHost", "バージョン {0} をダウンロードしました。適用するとゲームがセーブされ、メインメニューに戻り、このセーブでロビーがもう一度開きます。フレンドはいつも通り参加できます。「プレイ」を押すまでゲームは始まりません。", false),
            new("Multiplayer/Update/ReadyFriend", "バージョン {0} をダウンロードしました。適用するとメインメニューに戻り、もう一度ホストに参加します。", false),
            new("Multiplayer/Update/RestartLobby", "ロビーを再起動", false),
            new("Multiplayer/Update/OtherWays", "ほかの方法", false),
            new("Multiplayer/Update/RestartGameBody", "または、ゲームをセーブして再起動し、同じロビーを開きます。「あとで」を選ぶと、次にメインメニューに戻るかゲームを起動したときに適用されます。", false),
            new("Multiplayer/Update/RestartGame", "ゲームを再起動", false),
            new("Multiplayer/Update/Later", "あとで", false),
            new("Multiplayer/Update/Saving", "ゲームをセーブ中...", false),
            new("Multiplayer/Rejoin/Going", "ホストのもとへ戻っています...", false),
            new("Multiplayer/Scene/GameSpeaker", "ゲーム", false),
            new("Multiplayer/Scene/HoldTalk", "パートナーを待っています：パートナーがこの部屋に入ると会話が始まります。それまでは部屋の中を自由に動けます。扉は会話のあとに開きます。", false),
            new("Multiplayer/Scene/PartnerWaits", "パートナーがあなたを待っています：合流すると会話が始まります", false),
            new("Multiplayer/Update/Failed", "自動で更新できませんでした", false),
            new("Multiplayer/Update/OpenPage", "バージョン {0} のページを開いてダウンロードしますか？", false)
        ];
    }
}

/* EOF */
