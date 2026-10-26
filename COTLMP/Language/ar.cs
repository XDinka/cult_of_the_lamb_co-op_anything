/*
 * PROJECT:     Cult of the Lamb Multiplayer Mod
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     Arabic localization file
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

/* IMPORTS ********************************************************************/

using COTLMP;
using COTLMP.Api;
using COTLMP.Data;

/* TRANSLATION ****************************************************************/

namespace COTLMP.Language
{
    public class Arabic
    {
        public static LocalizationTable[] StringsTable =
        [
            new("UI/DLC", "اللعب الجماعي", true),
            new("UI/Banner", $"إصدار اللعب الجماعي v{COTLMP.Data.Version.CotlMpVer}", false),
            new("Multiplayer/UI/TitleDialog", "اللعب الجماعي", false),
            new("Multiplayer/UI/Settings", "إعدادات اللعب الجماعي", false),
            new("Multiplayer/UI/Settings/DisableMod", "تشغيل/إيقاف التعديل", false),
            new("Multiplayer/UI/Settings/PlayerName", "اسم اللاعب", false),
            new("Multiplayer/UI/Settings/PlayerCount", "الحد الأقصى لعدد اللاعبين", false),
            new("Multiplayer/UI/StartServer", "فتح اللعب عبر الإنترنت", false),
            new("Multiplayer/UI/SteamInviteHint", "يمكن لأصدقائك على Steam الانضمام عبر الدعوة (Shift+Tab) أو «الانضمام إلى اللعبة»", false),
            new("Multiplayer/UI/ServerStarted", "إيقاف الاستضافة", false),
            new("Multiplayer/UI/ServerConfirm", "إيقاف الاستضافة؟ سينقطع اتصال أصدقائك وتواصل أنت لعبتك.", false),
            new("Multiplayer/UI/LeaveServer", "مغادرة الخادم", false),
            new("Multiplayer/UI/LeaveConfirm", "هل تريد مغادرة الخادم؟", false),
            new("Multiplayer/UI/Disconnected", "انقطع الاتصال", false),
            new("Multiplayer/UI/DisconnectedError", "حدث خطأ (راجع وحدة التحكم)", false),
            new("Multiplayer/UI/Players", "اللاعبون", false),
            new("Multiplayer/UI/Host", "المضيف", false),
            new("Multiplayer/UI/You", "أنت", false),
            new("Multiplayer/UI/Ms", "مللي ث", false),
            new("Multiplayer/Steam/AfterPrologue", "يُفتح اللعب عبر الإنترنت بعد المقدّمة، في طائفتك", false),
            new("Multiplayer/Steam/LoadingHostSave", "جارٍ تحميل لعبة صديقك", false),
            new("Multiplayer/Steam/HostSaveFailed", "تعذّر تحميل لعبة صديقك", false),
            new("Multiplayer/Steam/Joined", "انضممت إلى صديقك", false),
            new("Multiplayer/Steam/JoinFailed", "تعذّر الوصول إلى صديقك", false),
            new("Multiplayer/Steam/HostLeft", "أوقف صديقك الاستضافة", false),
            new("Multiplayer/Reconnect/Connecting", "جارٍ الاتصال {0}/{1}", false),
            new("Multiplayer/Reconnect/Failed", "انقطع الاتصال بصديقك: تعذّرت إعادة الاتصال بعد 3 محاولات", false),
            new("Multiplayer/UI/PatchesFailedTitle", "Co-op Anything", false),
            new("Multiplayer/UI/PatchesFailed", "{0} من نقاط ربط التعديل لا تناسب إصدار اللعبة هذا: حُدّثت اللعبة بعد التعديل. قد لا يعمل اللعب التعاوني جيدًا حتى يصدر إصدار مُصلَح، وسيعرضه التعديل هنا فور صدوره", false),
            new("Multiplayer/UI/PatchesUpdate", "{0} من نقاط ربط التعديل لا تناسب إصدار اللعبة هذا: حُدّثت اللعبة بعد التعديل. صدر الإصدار {1} (لديك {2}). هل تريد التحديث الآن؟", false),
            new("Multiplayer/UI/PatchesOurs", "{0} من نقاط ربط التعديل لا تناسب إصدار لعبتك: قد لا يعمل اللعب التعاوني جيدًا", false),
            new("Multiplayer/UI/PatchesPartner", "{0}: {1} من نقاط ربط التعديل لا تناسب إصدار لعبته، قد لا يعمل اللعب التعاوني جيدًا", false),
            new("Multiplayer/Chat/Tip", "اضغط T للدردشة مع صديقك", false),
            new("Multiplayer/Chat/Hint", "رسالة… Enter للإرسال، Esc للإغلاق", false),
            new("Multiplayer/Steam/Left", "غادرت لعبة صديقك", false),
            new("Multiplayer/Steam/Stopped", "توقفت الاستضافة", false),
            new("Multiplayer/Steam/LeaveFirst", "أوقف الاستضافة لتنضم إلى صديقك", false),
            new("Multiplayer/Cult/FollowerBusy", "هناك من يتحدث مع هذا التابع بالفعل", false),
            new("Multiplayer/Scene/WaitPartner", "بانتظار دخول شريكك", false),
            new("Multiplayer/Scene/WaitScene", "بانتظار أن ينهي شريكك المشاهدة", false),
            new("Multiplayer/Scene/WaitSync", "بانتظار تأكيد المزامنة", false),
            new("Multiplayer/Version/Title", "إصدارات مختلفة من التعديل", false),
            new("Multiplayer/Version/Lines", "إصدار المضيف: {0}\nإصدارك: {1}", false),
            new("Multiplayer/Version/FriendLines", "إصدار صديقك: {0}\nإصدارك: {1}", false),
            new("Multiplayer/Version/Unknown", "غير معروف (0.2.8 أو أقدم)", false),
            new("Multiplayer/Version/Risk", "يمكنك اللعب، لكن اختلاف الإصدارات قد يسبب أخطاء أو حتى يتلف ملف الحفظ.", false),
            new("Multiplayer/Version/UpdateToHost", "التحديث إلى {0}؟ يُثبَّت التحديث هنا في القائمة الرئيسية، ثم تعود للانضمام إلى المضيف تلقائيًا.", false),
            new("Multiplayer/Version/HostOlder", "إصدار المضيف أقدم. يمكنك أن تطلب منه التحديث.", false),
            new("Multiplayer/Version/HostUpdate", "التحديث الآن؟ يُنزَّل أثناء لعبك، ثم تختار طريقة تثبيته: العودة إلى الردهة بهذا الحفظ أو إعادة تشغيل اللعبة.", false),
            new("Multiplayer/Version/FriendAsks", "يطلب منك صديقك تحديث التعديل", false),
            new("Multiplayer/Version/FriendHas", "تعديل صديقك {0}، وتعديلك {1}", false),
            new("Multiplayer/Version/Update", "تحديث", false),
            new("Multiplayer/Version/PlayAnyway", "العب على أي حال", false),
            new("Multiplayer/Version/Suggest", "اطلب من المضيف التحديث", false),
            new("Multiplayer/Version/NotNow", "ليس الآن", false),
            new("Multiplayer/Online/Title", "اللعب عبر الإنترنت", false),
            new("Multiplayer/Online/CreateLobby", "إنشاء ردهة", false),
            new("Multiplayer/Online/FriendsLobbies", "ردهات الأصدقاء", false),
            new("Multiplayer/Online/NoLobbies", "لا يوجد صديق لديه ردهة مفتوحة", false),
            new("Multiplayer/Online/Joining", "جارٍ الانضمام إلى {0}...", false),
            new("Multiplayer/Online/PickSave", "ملف الحفظ للردهة", false),
            new("Multiplayer/Online/Back", "رجوع", false),
            new("Multiplayer/Lobby/Title", "الردهة", false),
            new("Multiplayer/Lobby/NewGame", "لعبة جديدة", false),
            new("Multiplayer/Lobby/HostSave", "لعبة {0}: {1}", false),
            new("Multiplayer/Lobby/Play", "العب", false),
            new("Multiplayer/Lobby/Invite", "دعوة صديق", false),
            new("Multiplayer/Lobby/Leave", "مغادرة الردهة", false),
            new("Multiplayer/Lobby/Close", "إغلاق الردهة", false),
            new("Multiplayer/Lobby/WaitingFriend", "بانتظار صديق...", false),
            new("Multiplayer/Lobby/WaitingHost", "بانتظار أن يبدأ المضيف", false),
            new("Multiplayer/Lobby/Failed", "تعذّر فتح الردهة", false),
            new("Multiplayer/Intro/WaitCrown", "بانتظار أن يتسلّم صديقك التاج...", false),
            new("Multiplayer/UI/OnlineOpen", "اللعب عبر الإنترنت مفتوح", false),
            new("Multiplayer/UI/OnlineFailed", "تعذّر فتح اللعب عبر الإنترنت", false),
            new("Multiplayer/Steam/GameVersion", "لعبة صديقك بالإصدار {0}، ولعبتك بالإصدار {1}. حدّثا اللعبة على Steam كلاكما.", false),
            new("Multiplayer/Fox/HostDeals", "المضيف هو من يعقد الصفقات مع الثعلب", false),
            new("Multiplayer/Update/Title", "إصدار جديد من تعديل اللعب التعاوني", false),
            new("Multiplayer/Update/Body", "صدر الإصدار {0}، ولديك {1}. التحديث الآن؟ يُثبَّت هنا دون الخروج من اللعبة. فقط الإصدار الذي يحتاج BepInEx أو COTL API جديدًا يعيد تشغيل اللعبة من أجل برنامج التثبيت.", false),
            new("Multiplayer/Update/Downloading", "جارٍ تنزيل التحديث…", false),
            new("Multiplayer/Update/Screen", "جارٍ تحديث تعديل اللعب التعاوني إلى {0}", false),
            new("Multiplayer/Update/Progress", "تم تنزيل {0} من {1} كيلوبايت", false),
            new("Multiplayer/Update/Checking", "جارٍ التحقق من التنزيل", false),
            new("Multiplayer/Update/Starting", "جارٍ تشغيل الإصدار الجديد", false),
            new("Multiplayer/Update/Restart", "يحتاج هذا الإصدار إلى برنامج التثبيت: ستُعاد تشغيل اللعبة", false),
            new("Multiplayer/Update/Ready", "التحديث جاهز: سيبدأ في القائمة الرئيسية", false),
            new("Multiplayer/Update/Done", "تم تحديث تعديل اللعب التعاوني", false),
            new("Multiplayer/Update/ReadyTitle", "التحديث جاهز", false),
            new("Multiplayer/Update/ReadyHost", "تم تنزيل الإصدار {0}. لتثبيته تُحفظ اللعبة، وتنتقل إلى القائمة الرئيسية، وتُفتح الردهة من جديد بهذا الحفظ؛ وينضم صديقك كالمعتاد. لن يبدأ اللعب حتى تضغط العب.", false),
            new("Multiplayer/Update/ReadyFriend", "تم تنزيل الإصدار {0}. لتثبيته تنتقل إلى القائمة الرئيسية ثم تنضم إلى المضيف من جديد.", false),
            new("Multiplayer/Update/RestartLobby", "إعادة تشغيل الردهة", false),
            new("Multiplayer/Update/OtherWays", "خيارات أخرى", false),
            new("Multiplayer/Update/RestartGameBody", "أو تُحفظ اللعبة وتُعاد تشغيلها ثم تفتح الردهة نفسها. لاحقًا: يُثبَّت التحديث في المرة القادمة التي تكون فيها في القائمة الرئيسية أو تشغّل فيها اللعبة.", false),
            new("Multiplayer/Update/RestartGame", "إعادة تشغيل اللعبة", false),
            new("Multiplayer/Update/Later", "لاحقًا", false),
            new("Multiplayer/Update/Saving", "جارٍ حفظ اللعبة...", false),
            new("Multiplayer/Rejoin/Going", "جارٍ العودة إلى المضيف...", false),
            new("Multiplayer/Scene/GameSpeaker", "اللعبة", false),
            new("Multiplayer/Scene/HoldTalk", "بانتظار شريكك: تبدأ المحادثة فور دخوله هذه الغرفة. يمكنك التنقل داخل الغرفة في هذه الأثناء، وستُفتح أبوابها بعد المحادثة.", false),
            new("Multiplayer/Scene/PartnerWaits", "شريكك ينتظرك: تبدأ محادثة فور وصولك إليه", false),
            new("Multiplayer/Update/Failed", "تعذّر التحديث تلقائيًا", false),
            new("Multiplayer/Update/OpenPage", "فتح صفحة الإصدار {0} لتنزيله؟", false)
        ];
    }
}

/* EOF */
