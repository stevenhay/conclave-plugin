using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using MareSynchronos.FileCache;
using MareSynchronos.Localization;
using MareSynchronos.MareConfiguration;
using MareSynchronos.MareConfiguration.Models;
using MareSynchronos.Services;
using MareSynchronos.Services.Mediator;
using MareSynchronos.Services.ServerConfiguration;
using Microsoft.Extensions.Logging;
using System.Numerics;
using System.Text.RegularExpressions;

namespace MareSynchronos.UI;

public partial class IntroUi : WindowMediatorSubscriberBase
{
    private readonly MareConfigService _configService;
    private readonly CacheMonitor _cacheMonitor;
    private readonly Dictionary<string, string> _languages = new(StringComparer.Ordinal) { { "English", "en" }, { "Deutsch", "de" }, { "Français", "fr" } };
    private readonly ServerConfigurationManager _serverConfigurationManager;
    private readonly DalamudUtilService _dalamudUtilService;
    private readonly UiSharedService _uiShared;
    private int _currentLanguage;
    private bool _readFirstPage;

    private string _secretKey = string.Empty;
    private string _timeoutLabel = string.Empty;
    private Task? _timeoutTask;
    private string[]? _tosParagraphs;

    public IntroUi(ILogger<IntroUi> logger, UiSharedService uiShared, MareConfigService configService,
        CacheMonitor fileCacheManager, ServerConfigurationManager serverConfigurationManager, MareMediator mareMediator,
        PerformanceCollectorService performanceCollectorService, DalamudUtilService dalamudUtilService) : base(logger, mareMediator, "Conclave Setup", performanceCollectorService)
    {
        _uiShared = uiShared;
        _configService = configService;
        _cacheMonitor = fileCacheManager;
        _serverConfigurationManager = serverConfigurationManager;
        _dalamudUtilService = dalamudUtilService;
        IsOpen = false;
        ShowCloseButton = false;
        RespectCloseHotkey = false;

        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new Vector2(600, 400),
            MaximumSize = new Vector2(600, 2000),
        };

        GetToSLocalization();

        Mediator.Subscribe<SwitchToMainUiMessage>(this, (_) => IsOpen = false);
        Mediator.Subscribe<SwitchToIntroUiMessage>(this, (_) =>
        {
            _configService.Current.UseCompactor = !dalamudUtilService.IsWine;
            IsOpen = true;
        });
    }

    private int _prevIdx = -1;

    protected override void DrawInternal()
    {
        if (_uiShared.IsInGpose) return;

        if ((string.IsNullOrEmpty(_configService.Current.CacheFolder) || !_configService.Current.InitialScanComplete || !Directory.Exists(_configService.Current.CacheFolder)))
        {
            using (_uiShared.UidFont.Push())
                ImGui.TextUnformatted("File Storage Setup");

            ImGui.Separator();

            if (!_uiShared.HasValidPenumbraModPath)
            {
                UiSharedService.ColorTextWrapped("You do not have a valid Penumbra path set. Open Penumbra and set up a valid path for the mod directory.", ImGuiColors.DalamudRed);
            }
            else
            {
                UiSharedService.TextWrapped("To not unnecessary download files already present on your computer, Conclave will have to scan your Penumbra mod directory. " +
                                     "Additionally, a local storage folder must be set where Conclave will download other character files to. " +
                                     "Once the storage folder is set and the scan complete, this page will automatically forward to registration at a service.");
                UiSharedService.TextWrapped("Note: The initial scan, depending on the amount of mods you have, might take a while. Please wait until it is completed.");
                UiSharedService.ColorTextWrapped("Warning: once past this step you should not delete the FileCache.csv of Conclave in the Plugin Configurations folder of Dalamud. " +
                                          "Otherwise on the next launch a full re-scan of the file cache database will be initiated.", ImGuiColors.DalamudYellow);
                UiSharedService.ColorTextWrapped("Warning: if the scan is hanging and does nothing for a long time, chances are high your Penumbra folder is not set up properly.", ImGuiColors.DalamudYellow);
                _uiShared.DrawCacheDirectorySetting();
            }

            if (!_cacheMonitor.IsScanRunning && !string.IsNullOrEmpty(_configService.Current.CacheFolder) && _uiShared.HasValidPenumbraModPath && Directory.Exists(_configService.Current.CacheFolder))
            {
                if (ImGui.Button("Start Scan##startScan"))
                {
                    _cacheMonitor.InvokeScan();
                }
            }
            else
            {
                _uiShared.DrawFileScanState();
            }
            if (!_dalamudUtilService.IsWine)
            {
                var useFileCompactor = _configService.Current.UseCompactor;
                if (ImGui.Checkbox("Use File Compactor", ref useFileCompactor))
                {
                    _configService.Current.UseCompactor = useFileCompactor;
                    _configService.Save();
                }
                UiSharedService.ColorTextWrapped("The File Compactor can save a tremendeous amount of space on the hard disk for downloads through Conclave. It will incur a minor CPU penalty on download but can speed up " +
                    "loading of other characters. It is recommended to keep it enabled. You can change this setting later anytime in the Conclave settings.", ImGuiColors.DalamudYellow);
            }
        }
        else if (!_uiShared.ApiController.ServerAlive)
        {
            using (_uiShared.UidFont.Push())
                ImGui.TextUnformatted("Service Registration");
            ImGui.Separator();
            UiSharedService.TextWrapped("To be able to use Conclave you will have to request an account.");
            UiSharedService.TextWrapped("If you have an authorisation key please enter it below.");

            UiSharedService.DistanceSeparator();

            int serverIdx = 0;
            var selectedServer = _serverConfigurationManager.GetServerByIndex(serverIdx);

            using (var node = ImRaii.TreeNode("Advanced Options"))
            {
                if (node)
                {
                    serverIdx = _uiShared.DrawServiceSelection(selectOnChange: true, showConnect: false);
                    if (serverIdx != _prevIdx)
                    {
                        _prevIdx = serverIdx;
                    }
                }
            }

            var text = "Enter Secret Key";
            var buttonText = "Save";
            var buttonWidth = _secretKey.Length != 64 ? 0 : ImGuiHelpers.GetButtonSize(buttonText).X + ImGui.GetStyle().ItemSpacing.X;
            var textSize = ImGui.CalcTextSize(text);

            ImGuiHelpers.ScaledDummy(5);
            ImGuiHelpers.ScaledDummy(5);

            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(text);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(UiSharedService.GetWindowContentRegionWidth() - ImGui.GetWindowContentRegionMin().X - buttonWidth - textSize.X);
            ImGui.InputText("", ref _secretKey, 64);
            if (_secretKey.Length > 0 && _secretKey.Length != 64)
            {
                UiSharedService.ColorTextWrapped("Your secret key must be exactly 64 characters long. Don't enter your Lodestone auth here.", ImGuiColors.DalamudRed);
            }
            else if (_secretKey.Length == 64 && !HexRegex().IsMatch(_secretKey))
            {
                UiSharedService.ColorTextWrapped("Your secret key can only contain ABCDEF and the numbers 0-9.", ImGuiColors.DalamudRed);
            }
            else if (_secretKey.Length == 64)
            {
                ImGui.SameLine();
                if (ImGui.Button(buttonText))
                {
                    if (_serverConfigurationManager.CurrentServer == null) _serverConfigurationManager.SelectServer(0);
                    if (!_serverConfigurationManager.CurrentServer!.SecretKeys.Any())
                    {
                        _serverConfigurationManager.CurrentServer!.SecretKeys.Add(_serverConfigurationManager.CurrentServer.SecretKeys.Select(k => k.Key).LastOrDefault() + 1, new SecretKey()
                        {
                            FriendlyName = $"Secret Key added on Setup ({DateTime.Now:yyyy-MM-dd})",
                            Key = _secretKey,
                        });
                        _serverConfigurationManager.AddCurrentCharacterToServer();
                    }
                    else
                    {
                        _serverConfigurationManager.CurrentServer!.SecretKeys[0] = new SecretKey()
                        {
                            FriendlyName = $"Secret Key added on Setup ({DateTime.Now:yyyy-MM-dd})",
                            Key = _secretKey,
                        };
                    }
                    _secretKey = string.Empty;
                    _ = Task.Run(() => _uiShared.ApiController.CreateConnectionsAsync());
                }
            }
        }
        else
        {
            Mediator.Publish(new SwitchToMainUiMessage());
            IsOpen = false;
        }
    }

    private void GetToSLocalization(int changeLanguageTo = -1)
    {
        if (changeLanguageTo != -1)
        {
            _uiShared.LoadLocalization(_languages.ElementAt(changeLanguageTo).Value);
        }

        _tosParagraphs = [Strings.ToS.Paragraph1, Strings.ToS.Paragraph2, Strings.ToS.Paragraph3, Strings.ToS.Paragraph4, Strings.ToS.Paragraph5, Strings.ToS.Paragraph6];
    }

    [GeneratedRegex("^([A-F0-9]{2})+")]
    private static partial Regex HexRegex();
}