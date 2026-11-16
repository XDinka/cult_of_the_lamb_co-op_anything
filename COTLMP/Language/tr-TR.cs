/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Turkish localization file
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP;
using COTLMP.Api;
using COTLMP.Data;

/* TRANSLATION ****************************************************************/

namespace COTLMP.Language
{
    public class Turkish
    {
        public static LocalizationTable[] StringsTable =
        [
            new("UI/DLC", "Çok Oyunculu", true),
            new("UI/Banner", $"Çok Oyunculu Sürüm v{COTLMP.Data.Version.CotlMpVer}", false),
            new("Multiplayer/UI/TitleDialog", "Çok Oyunculu", false),
            new("Multiplayer/UI/Settings", "Çok Oyunculu Ayarları", false),
            new("Multiplayer/UI/Settings/DisableMod", "Modu aç/kapat", false),
            new("Multiplayer/UI/Settings/PlayerName", "Oyuncu adı", false),
            new("Multiplayer/UI/StartServer", "Çevrim içi aç", false),
            new("Multiplayer/UI/SteamInviteHint", "Steam arkadaşların Shift+Tab davetleriyle ya da \"Oyuna katıl\" ile katılabilir", false),
            new("Multiplayer/UI/ServerStarted", "Ev sahipliğini bitir", false),
            new("Multiplayer/UI/ServerConfirm", "Ev sahipliği bitsin mi? Arkadaşlarının bağlantısı kesilir, sen kendi oyununa devam edersin.", false),
            new("Multiplayer/UI/LeaveServer", "Sunucudan ayrıl", false),
            new("Multiplayer/UI/LeaveConfirm", "Sunucudan ayrılmak istiyor musun?", false),
            new("Multiplayer/UI/Disconnected", "Bağlantı kesildi", false),
            new("Multiplayer/UI/DisconnectedError", "Bir hata oluştu (konsola bak)", false),
            new("Multiplayer/UI/Players", "Oyuncular", false),
            new("Multiplayer/UI/Host", "ev sahibi", false),
            new("Multiplayer/UI/You", "sen", false),
            new("Multiplayer/UI/Ms", "ms", false),
            new("Multiplayer/Steam/AfterPrologue", "Çevrim içi oyun, girişten sonra tarikatında açılır", false),
            new("Multiplayer/Steam/LoadingHostSave", "Arkadaşının oyunu yükleniyor", false),
            new("Multiplayer/Steam/HostSaveFailed", "Arkadaşının oyunu yüklenemedi", false),
            new("Multiplayer/Steam/Joined", "Arkadaşına katıldın", false),
            new("Multiplayer/Steam/JoinFailed", "Arkadaşına ulaşılamadı", false),
            new("Multiplayer/Steam/HostLeft", "Arkadaşın ev sahipliğini bitirdi", false),
            new("Multiplayer/Reconnect/Connecting", "Bağlanıyor {0}/{1}", false),
            new("Multiplayer/Reconnect/Failed", "Arkadaşınla bağlantı koptu: 3 denemede yeniden bağlanılamadı", false),
            new("Multiplayer/Reconnect/CancelHint", "Esc: denemeyi bırak", false),
            new("Multiplayer/Reconnect/Cancel", "Yeniden bağlanmayı durdur", false),
            new("Multiplayer/Reconnect/Cancelled", "Yeniden bağlanma iptal edildi", false),
            new("Multiplayer/UI/PatchesFailedTitle", "Co-op Anything", false),
            new("Multiplayer/UI/PatchesFailed", "Modun {0} bağlantı noktası bu oyun sürümüne uymuyor: oyun moddan sonra güncellendi. Düzeltilmiş sürüm çıkana kadar ortak oyun düzgün çalışmayabilir; mod, çıkar çıkmaz burada önerecek", false),
            new("Multiplayer/UI/PatchesUpdate", "Modun {0} bağlantı noktası bu oyun sürümüne uymuyor: oyun moddan sonra güncellendi. {1} sürümü çıktı (sende {2} var). Şimdi güncellensin mi?", false),
            new("Multiplayer/UI/PatchesOurs", "Modun {0} bağlantı noktası oyun sürümüne uymuyor: ortak oyun düzgün çalışmayabilir", false),
            new("Multiplayer/UI/PatchesPartner", "{0}: modun {1} bağlantı noktası onun oyun sürümüne uymuyor, ortak oyun düzgün çalışmayabilir", false),
            new("Multiplayer/UI/CopyLog", "Günlüğü kopyala", false),
            new("Multiplayer/UI/LogCopied", "Kopyalandı", false),
            new("Multiplayer/Online/Discord", "Hatalar ve öneriler", false),
            new("Multiplayer/Chat/Tip", "Arkadaşınla sohbet etmek için T'ye bas", false),
            new("Multiplayer/Chat/Hint", "Mesaj… Enter gönderir, Esc kapatır", false),
            new("Multiplayer/Steam/Left", "Arkadaşının oyunundan ayrıldın", false),
            new("Multiplayer/Steam/Stopped", "Ev sahipliği bitti", false),
            new("Multiplayer/Steam/LeaveFirst", "Arkadaşına katılmak için ev sahipliğini bitir", false),
            new("Multiplayer/Cult/FollowerBusy", "Biri zaten bu müritle konuşuyor", false),
            new("Multiplayer/Scene/WaitPartner", "Ortağının içeri girmesi bekleniyor", false),
            new("Multiplayer/Scene/WaitScene", "Ortağının izlemeyi bitirmesi bekleniyor", false),
            new("Multiplayer/Scene/WaitSync", "Eşitlemenin onaylanması bekleniyor", false),
            new("Multiplayer/Version/Title", "Farklı mod sürümleri", false),
            new("Multiplayer/Version/Lines", "Ev sahibinin sürümü: {0}\nSenin sürümün: {1}", false),
            new("Multiplayer/Version/FriendLines", "Arkadaşının sürümü: {0}\nSenin sürümün: {1}", false),
            new("Multiplayer/Version/Unknown", "bilinmiyor (0.2.8 veya daha eski)", false),
            new("Multiplayer/Version/Risk", "Oynayabilirsin, ama farklı sürümler hatalara yol açabilir, hatta kayıt dosyasına zarar verebilir.", false),
            new("Multiplayer/Version/UpdateToHost", "{0} sürümüne güncellensin mi? Güncelleme burada, ana menüde yüklenir, sonra ev sahibine kendiliğinden yeniden katılırsın.", false),
            new("Multiplayer/Version/HostOlder", "Ev sahibinin sürümü daha eski. Güncellemesini isteyebilirsin.", false),
            new("Multiplayer/Version/HostUpdate", "Şimdi güncellensin mi? Sen oynarken indirilir; sonra nasıl yükleneceğini seçersin: bu kayıtla lobiye dönmek ya da oyunu yeniden başlatmak.", false),
            new("Multiplayer/Version/FriendAsks", "Arkadaşın modu güncellemeni istiyor", false),
            new("Multiplayer/Version/FriendHas", "Arkadaşının modu {0}, seninki {1}", false),
            new("Multiplayer/Version/FriendRefused", "Arkadaşının modu {0}. 0.9.4'ten eski sürümler DLC'lerin verdiklerini kayıt dosyandan siler, bu yüzden arkadaşın katılamaz. Oyunun ana menüsünden güncellemesini iste.", false),
            new("Multiplayer/Version/HostStrips", "Ev sahibinin modu ({0}), DLC'lerin verdiklerini kendi kayıt dosyasından siler.", false),
            new("Multiplayer/Version/Update", "Güncelle", false),
            new("Multiplayer/Version/PlayAnyway", "Yine de oyna", false),
            new("Multiplayer/Version/Suggest", "Ev sahibinden güncellemesini iste", false),
            new("Multiplayer/Version/NotNow", "Şimdi değil", false),
            new("Multiplayer/Online/Title", "Çevrim İçi Oyun", false),
            new("Multiplayer/Online/CreateLobby", "Lobi oluştur", false),
            new("Multiplayer/Online/FriendsLobbies", "Arkadaşların lobileri", false),
            new("Multiplayer/Online/NoLobbies", "Hiçbir arkadaşının açık lobisi yok", false),
            new("Multiplayer/Online/Joining", "{0} lobisine katılınıyor...", false),
            new("Multiplayer/Online/PickSave", "Lobi için kayıt", false),
            new("Multiplayer/Online/Back", "Geri", false),
            new("Multiplayer/Lobby/Title", "Lobi", false),
            new("Multiplayer/Lobby/NewGame", "Yeni oyun", false),
            new("Multiplayer/Lobby/HostSave", "{0} adlı oyuncunun oyunu: {1}", false),
            new("Multiplayer/Lobby/Play", "Oyna", false),
            new("Multiplayer/Lobby/Invite", "Arkadaş davet et", false),
            new("Multiplayer/Lobby/Leave", "Lobiden ayrıl", false),
            new("Multiplayer/Lobby/Close", "Lobiyi kapat", false),
            new("Multiplayer/Lobby/WaitingFriend", "Bir arkadaş bekleniyor...", false),
            new("Multiplayer/Lobby/WaitingHost", "Ev sahibinin başlatması bekleniyor", false),
            new("Multiplayer/Lobby/Failed", "Lobi açılamadı", false),
            new("Multiplayer/Online/JoinFailed", "Arkadaşının lobisine ulaşılamadı", false),
            new("Multiplayer/Intro/WaitCrown", "Arkadaşının tacı alması bekleniyor...", false),
            new("Multiplayer/UI/OnlineOpen", "Çevrim içi oyun açık", false),
            new("Multiplayer/UI/OnlineFailed", "Çevrim içi oyun açılamadı", false),
            new("Multiplayer/Steam/GameVersion", "Arkadaşının oyunu {0} sürümünde, seninki {1}. Oyunu iki tarafta da Steam'den güncelleyin.", false),
            new("Multiplayer/Fox/HostDeals", "Tilkiyle pazarlığı ev sahibi yapar", false),
            new("Multiplayer/Update/Title", "Ortak oyun modunun yeni sürümü", false),
            new("Multiplayer/Update/Body", "{0} sürümü çıktı, sende {1} var. Şimdi güncellensin mi? Oyundan çıkmadan burada yüklenir. Yalnızca yeni bir BepInEx veya COTL API gerektiren sürüm, yükleyici için oyunu yeniden başlatır.", false),
            new("Multiplayer/Update/Downloading", "Güncelleme indiriliyor…", false),
            new("Multiplayer/Update/Screen", "Ortak oyun modu {0} sürümüne güncelleniyor", false),
            new("Multiplayer/Update/Progress", "İndirilen: {0} / {1} KB", false),
            new("Multiplayer/Update/Checking", "İndirme denetleniyor", false),
            new("Multiplayer/Update/Starting", "Yeni sürüm başlatılıyor", false),
            new("Multiplayer/Update/Restart", "Bu sürüm yükleyici gerektiriyor: oyun yeniden başlayacak", false),
            new("Multiplayer/Update/Ready", "Güncelleme hazır: ana menüde başlayacak", false),
            new("Multiplayer/Update/Done", "Ortak oyun modu güncellendi", false),
            new("Multiplayer/Update/ReadyTitle", "Güncelleme hazır", false),
            new("Multiplayer/Update/ReadyHost", "{0} sürümü indirildi. Yüklemek için oyun kaydedilir, ana menüye geçersin ve lobi bu kayıtla yeniden açılır; arkadaşın her zamanki gibi katılır. Oyna'ya basana kadar hiçbir şey başlamaz.", false),
            new("Multiplayer/Update/ReadyFriend", "{0} sürümü indirildi. Yüklemek için ana menüye geçer ve ev sahibine yeniden katılırsın.", false),
            new("Multiplayer/Update/RestartLobby", "Lobiyi yeniden başlat", false),
            new("Multiplayer/Update/OtherWays", "Diğer seçenekler", false),
            new("Multiplayer/Update/RestartGameBody", "Ya da oyun kaydedilir, yeniden başlar ve aynı lobiyi açar. Sonra: güncelleme, bir dahaki sefer ana menüde olduğunda ya da oyunu başlattığında yüklenir.", false),
            new("Multiplayer/Update/RestartGame", "Oyunu yeniden başlat", false),
            new("Multiplayer/Update/Later", "Sonra", false),
            new("Multiplayer/Update/Saving", "Oyun kaydediliyor...", false),
            new("Multiplayer/Rejoin/Going", "Ev sahibine dönülüyor...", false),
            new("Multiplayer/Scene/GameSpeaker", "Oyun", false),
            new("Multiplayer/Scene/HoldTalk", "Ortağın bekleniyor: konuşma, o bu odaya girer girmez başlar. Bu sırada odada dolaşabilirsin; kapılar konuşmadan sonra açılır.", false),
            new("Multiplayer/Scene/PartnerWaits", "Ortağın seni bekliyor: yanına vardığında bir konuşma başlayacak", false),
            new("Multiplayer/Scene/WaitHostLead", "Ev sahibinin oyunu geride kaldı: yetişir yetişmez konuşma devam edecek", false),
            new("Multiplayer/Update/Failed", "Kendiliğinden güncellenemedi", false),
            new("Multiplayer/Update/OpenPage", "İndirmek için {0} sürümünün sayfası açılsın mı?", false)
        ];
    }
}

/* EOF */
