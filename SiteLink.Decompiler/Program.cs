using SiteLink.Decompiler.Core;

string assemblyPath = @"D:\SteamLibrary\steamapps\common\SCP Secret Laboratory Dedicated Server\SCPSL_Data\Managed\Assembly-CSharp.dll";
string outputDirectory = @"D:\VS Projects\SiteLink\SiteLink.Protocol";

var plan = new DecompilationPlan()
    //.AddType(new TypeExportOptions("CollectionExtensions")
    //    .AddMethod("IndexOf", null))

    .AddType(new TypeExportOptions("PlayerStatsSystem.AdminFlags"))

    .AddType(new TypeExportOptions("Utils.Networking.NetworkUtils"))
    .AddType(new TypeExportOptions("AdminToys.PrimitiveFlags"))
    .AddType(new TypeExportOptions("Hints.HintMessage"))
    .AddType(new TypeExportOptions("RejectionReason"))
    .AddType(new TypeExportOptions("PlayerRoles.RoleTypeId"))

    .AddType(
        new TypeExportOptions("RelativePositioning.RelativePositionSerialization")
            .AddMethod("WriteRelativePosition", null, "NetworkWriter", "RelativePosition")
            .AddMethod("ReadRelativePosition", null, "NetworkReader")
    )

    .AddType(
        new TypeExportOptions("RelativePositioning.RelativePosition")
            .AddField("WaypointId")
            .AddField("PositionX")
            .AddField("PositionY")
            .AddField("PositionZ")
            .AddConstructor("NetworkReader")
            .AddAllMethods()

    ).AddType(
        new TypeExportOptions("UserSettings.ServerSpecific.ServerSpecificSettingsSync")
            .AddField("AllSettingConstructors")
            .AddField("_allTypes")
            .AddProperty("DefinedSettings")
            .AddProperty("AllSettingTypes")
            .AddMethod("GetTypeFromCode", null, "byte")
            .AddMethod("GetCodeFromType", null, "Type")
            .AddMethod("CreateInstance", null, "Type")


    ).AddType(
        new TypeExportOptions("PlayerStatsSystem.SyncedStatMessages")
            .AddNestedType("StatMessageType")

    ).AddType(
        new TypeExportOptions("UserSettings.ServerSpecific.SSSEntriesPack")
            .AddField("Settings")
            .AddField("Version")
            .AddMethod("Serialize", null, "NetworkWriter")
            .AddConstructor("ServerSpecificSettingBase[]", "int")

    ).AddType(
        new TypeExportOptions("UserSettings.ServerSpecific.ServerSpecificSettingBase")
            .AddProperty("SettingId")
            .AddProperty("PlayerPrefsKey")
            .AddProperty("CollectionId")
            .AddProperty("Label")
            .AddProperty("HintDescription")
            .AddProperty("IsServerOnly")
            .AddMethod("ApplyDefaultValues")
            .AddMethod("TransferValue", null, "ServerSpecificSettingBase")
            .AddMethod("SetId", null, "int?", "string")
            .AddMethod("SerializeEntry", null, "NetworkWriter")

    ).AddType(
        new TypeExportOptions("UserSettings.ServerSpecific.SSButton")
            .AddField("SyncLastPress")
            .AddProperty("ButtonText")
            .AddProperty("HoldTimeSeconds")
            .AddMethod("ApplyDefaultValues")
            .AddConstructor("int?", "string", "string", "float?", "string")

    ).AddType(
        new TypeExportOptions("UserSettings.ServerSpecific.SSGroupHeader")
            .AddProperty("ReducedPadding")
            .AddMethod("ApplyDefaultValues")
            .AddConstructor("int?", "string", "bool", "string")
            .AddConstructor("string", "bool", "string")

    ).AddType(
        new TypeExportOptions("UserSettings.ServerSpecific.SSKeybindSetting")
            .AddProperty("SuggestedKey")
            .AddProperty("PreventInteractionOnGUI")
            .AddProperty("AllowSpectatorTrigger")
            .AddProperty("SyncIsPressed")
            .AddMethod("ApplyDefaultValues")
            .AddConstructor("int?", "string", "KeyCode", "bool", "bool", "string", "byte")

    ).AddType(
        new TypeExportOptions("UserSettings.ServerSpecific.SSDropdownSetting")
            .AddNestedType("DropdownEntryType")
            .AddProperty("EntryType")
            .AddProperty("DefaultOptionIndex")
            .AddProperty("SyncSelectionIndexRaw")
            .AddProperty("Options")
            .AddMethod("ApplyDefaultValues")
            .AddConstructor("int?", "string", "string[]", "int", "DropdownEntryType", "string", "byte", "bool")

    ).AddType(
        new TypeExportOptions("UserSettings.ServerSpecific.SSTwoButtonsSetting")
            .AddProperty("OptionA")
            .AddProperty("OptionB")
            .AddProperty("DefaultIsB")
            .AddProperty("SyncIsB")
            .AddMethod("ApplyDefaultValues")
            .AddConstructor("int?", "string", "string", "string", "bool", "string", "byte", "bool")

    ).AddType(
        new TypeExportOptions("UserSettings.ServerSpecific.SSSliderSetting")
            .AddProperty("SyncFloatValue")
            .AddProperty("DefaultValue")
            .AddProperty("MinValue")
            .AddProperty("MaxValue")
            .AddProperty("Integer")
            .AddProperty("ValueToStringFormat")
            .AddProperty("FinalDisplayFormat")
            .AddMethod("ApplyDefaultValues")
            .AddConstructor("int?", "string", "float", "float", "float", "bool", "string", "string", "string", "byte", "bool")

    ).AddType(
        new TypeExportOptions("UserSettings.ServerSpecific.SSPlaintextSetting")
            .AddProperty("Placeholder")
            .AddProperty("CharacterLimit")
            .AddProperty("ContentType")
            .AddProperty("SyncInputText")
            .AddProperty("DefaultText")
            .AddMethod("ApplyDefaultValues")
            .AddConstructor("int?", "string", "string", "int", "TMP_InputField.ContentType", "string", "byte", "bool")

    ).AddType(
        new TypeExportOptions("UserSettings.ServerSpecific.SSTextArea")
            .AddProperty("Foldout")
            .AddProperty("AlignmentOptions")
            .AddNestedType("FoldoutMode")
            .AddMethod("ApplyDefaultValues")
            .AddConstructor("int?", "string", "FoldoutMode", "string", "TextAlignmentOptions")

    ).AddType(
        new TypeExportOptions("AlphaWarheadSyncInfoSerializer")
        .AddAllMethods()
    ).AddType(
        new TypeExportOptions("RecyclablePlayerIdReaderWriter")
        .AddAllMethods()
    ).AddType(
        new TypeExportOptions("AlphaWarheadSyncInfo")
        .KeepWholeType()
    ).AddType(
        new TypeExportOptions("WarheadScenarioType")
        .KeepWholeType()
    ).AddType(
        new TypeExportOptions("LightContainmentZoneDecontamination.DecontaminationController")
        .AddNestedType("DecontaminationStatus")
    ).AddType(
        new TypeExportOptions("InventorySystem.Items.ItemIdentifier")
        .KeepWholeType()
    ).AddType(
        new TypeExportOptions("InventorySystem.Items.ItemBase")
        .AddField("ItemTypeId")
        .AddProperty("ItemSerial")
    ).AddType(
        new TypeExportOptions("ItemType")
        .KeepWholeType()
    ).AddType(
        new TypeExportOptions("PlayerInfoArea")
        .KeepWholeType()
    ).AddType(
        new TypeExportOptions("RecyclablePlayerId")
        .KeepWholeType()
    ).AddType(
        new TypeExportOptions("CentralAuthPreauthFlags")
        .KeepWholeType()
    ).AddType(
        new TypeExportOptions("RoundRestarting.RoundRestartType")
        .KeepWholeType()
    ).AddType(
        new TypeExportOptions("ChallengeType")
        .KeepWholeType()
    ).AddType(
        new TypeExportOptions("PlayerRoles.PlayerRoleEnumsReadersWriters")
        .KeepWholeType()
    );

var exporter = new SelectiveTypeExporter(assemblyPath, outputDirectory);
exporter.Export(plan);