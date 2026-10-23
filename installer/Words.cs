/*
 * PROJECT:     Cult of the Lamb Co-op Anything installer
 * LICENSE:     MIT (https://spdx.org/licenses/MIT)
 * PURPOSE:     The installer's words, in Russian or English by the system language
 * COPYRIGHT:	Copyright 2026 COTLMP contributors
 */

using System.Globalization;

namespace CotlCoopInstaller
{
    internal static class Words
    {
        internal static bool Russian = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName is "ru" or "uk" or "be" or "kk";

        private static string T(string Ru, string En) => Russian ? Ru : En;

        internal static string Kicker => "CULT OF THE LAMB";
        internal static string Heading => T("Онлайн-кооператив", "Online Co-op");
        internal static string Sub(string Version) => T($"Через Steam, без IP и портов · версия {Version}", $"Over Steam, no IPs or ports · version {Version}");
        internal static string Folder => T("Папка с игрой", "Game folder");
        internal static string Browse => T("Обзор…", "Browse…");
        internal static string Install => T("Установить", "Install");
        internal static string Update => T("Обновить", "Update");
        internal static string Uninstall => T("Удалить мод", "Remove mod");
        internal static string Launch => T("Запустить игру", "Play");
        internal static string Close => T("Закрыть", "Close");
        internal static string PickFolder => T("Выберите папку Cult of the Lamb (в ней лежит «Cult Of The Lamb.exe»)", "Pick the Cult of the Lamb folder (the one with \"Cult Of The Lamb.exe\")");
        internal static string FoundSteam(string Build) => T($"Игра найдена в Steam (сборка {Build}).", $"Found the game in Steam (build {Build}).");
        internal static string NotFound => T("Игра не найдена в Steam. Укажите её папку вручную.", "The game wasn't found in Steam. Pick its folder.");
        internal static string NotGame => T("В этой папке нет «Cult Of The Lamb.exe».", "There's no \"Cult Of The Lamb.exe\" in this folder.");
        internal static string OtherBuild(string Build) => T(
            $"Внимание: у вас сборка {Build}, мод сделан для {Installer.ExpectedBuild} (версия {Installer.ExpectedVersion}). Может не заработать.",
            $"Careful: your build is {Build}, the mod was made for {Installer.ExpectedBuild} (version {Installer.ExpectedVersion}). It may not work.");
        internal static string AlreadyInstalled => T("Мод уже установлен. Можно обновить или удалить.", "The mod is installed. You can update or remove it.");
        internal static string Ready => T("Всё готово к установке.", "Ready to install.");
        internal static string CloseGame => T("Сначала закройте Cult of the Lamb.", "Close Cult of the Lamb first.");
        internal static string Installing => T("Устанавливаем", "Installing");
        internal static string Removing => T("Удаляем", "Removing");
        internal static string Installed(int Count) => T($"Готово: {Count} файлов. Запускайте игру из Steam как обычно.", $"Done: {Count} files. Start the game from Steam as usual.");
        internal static string Removed => T("Мод удалён, заменённые файлы возвращены на место.", "The mod is removed, replaced files are back.");
        internal static string Failed(string Why) => T($"Не получилось: {Why}", $"It didn't work: {Why}");
        internal static string NeedAdmin => T("Для этой папки нужны права администратора.", "This folder needs administrator rights.");
        internal static string HowTo => T(
            "Мод ставят оба игрока. Хост: загрузите сохранение, Esc › «Open to friends», пригласите друга.\nДруг: примите приглашение в Steam и загрузите своё сохранение.",
            "Both players install the mod. Host: load a save, Esc › \"Open to friends\", invite your friend.\nFriend: accept the Steam invite and load your own save.");
        internal static string Footer => "github.com/XDinka/cult_of_the_lamb_co-op_anything · MIT · " + T("фанатский мод, не связан с Massive Monster и Devolver", "fan mod, not affiliated with Massive Monster or Devolver");
    }
}
