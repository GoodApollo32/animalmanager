using Vintagestory.API.Common;

// Embed the mod metadata directly in the assembly so a bare `animalmanager.dll` dropped into
// VintagestoryData/Mods/ loads without needing a separate modinfo.json next to it. When the
// mod is loaded from the packaged zip/folder, the root modinfo.json is used instead — the two
// are kept in sync (same modid/version/side).
[assembly: ModInfo(
    "Animal Manager",
    "animalmanager",
    Version = "0.1.0",
    Side = "Client",
    RequiredOnClient = true,
    RequiredOnServer = false,
    Description = "Herd overview panel: see every captive animal near you and their husbandry stats at a glance. Toggle which stats are shown.",
    Authors = new[] { "GoodApollo32" }
)]
[assembly: ModDependency("game")]
[assembly: ModDependency("survival")]
