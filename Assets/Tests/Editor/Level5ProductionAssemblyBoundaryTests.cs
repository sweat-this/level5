using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Phase 2d architecture guards (docs/systems-restructure-plan.md, "Phase 2 - Assembly split").
///
/// Reads source as text rather than referencing it, the same trick <see cref="Level5GameManagerEdgeTests"/>
/// uses: this file has no asmdef of its own, so it can see every folder - including the still-blocked
/// basketball/game-manager legs (the player leg closed in Slice 51 - see
/// <see cref="NoProductionSourceExistsDirectlyUnderThePlayerRootOutsideLevel5Player"/>) - without
/// becoming part of the dependency graph it checks.
///
/// The production asmdef list is discovered at run time (every runtime, non-test .asmdef under
/// Assets/Scripts and Assets/Level5), not hard-coded, so this guard covers every future 2b slice
/// automatically rather than needing an allowlist maintained by hand.
/// </summary>
public class Level5ProductionAssemblyBoundaryTests
{
    private static readonly string AssetsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
    private static readonly string ScriptsRoot = Path.Combine(AssetsRoot, "Scripts");
    private static readonly string Level5Root = Path.Combine(AssetsRoot, "Level5");

    /// <summary>
    /// Package/engine assemblies proven Editor-only and therefore unsafe for a runtime asmdef to
    /// reference. "Analytics" is the 2a canary's finding (systems-restructure-plan.md): it is the
    /// asmdef-based com.unity.analytics Editor-window code (`includePlatforms: ["Editor"]`), not the
    /// UnityEngine.Analytics engine module runtime code actually uses - which needs no reference.
    /// </summary>
    private static readonly string[] EditorOnlyPackageAssemblies = { "Analytics" };

    /// <summary>
    /// Discovered once per test run and reused by every test in this fixture - each test that reads
    /// it would otherwise re-walk both script roots and re-read every .asmdef from disk.
    /// </summary>
    private static readonly Lazy<List<AsmdefInfo>> ProductionAssemblies =
        new Lazy<List<AsmdefInfo>>(DiscoverProductionAssemblies);

    [Test]
    public void NoMigratedProductionAssemblyReachesIntoAssemblyCSharp()
    {
        List<AsmdefInfo> assemblies = ProductionAssemblies.Value;
        List<string> migratedFolders = assemblies.Select(a => a.Folder).ToList();
        Assert.That(
            migratedFolders,
            Is.Not.Empty,
            "expected at least the leaf assemblies Phase 2b already migrated - if this is empty, "
            + "the discovery logic itself is broken, not the codebase");

        HashSet<string> assemblyCSharpTypes = CollectDeclaredTypeNames(EnumerateAssemblyCSharpScripts());

        List<string> offenders = new List<string>();
        foreach (string file in EnumerateFilesUnder(migratedFolders))
        {
            HashSet<string> usedIdentifiers = UsedIdentifiers(file);
            List<string> hits = usedIdentifiers.Where(assemblyCSharpTypes.Contains).ToList();
            if (hits.Count > 0)
            {
                offenders.Add($"{Level5TestSourceText.Relative(file)}: {string.Join(", ", hits)}");
            }
        }

        Assert.That(
            offenders,
            Is.Empty,
            "these files live in a production asmdef but reach for a type still declared in "
            + "Assembly-CSharp - either that type needs to migrate too, or this file does not belong "
            + "in this assembly yet (docs/systems-restructure-plan.md, Phase 2b0's hard rule):\n"
            + string.Join("\n", offenders));
    }

    // A leaf-to-leaf variant of NoMigratedProductionAssemblyReachesIntoAssemblyCSharp (checking that
    // a production assembly doesn't reach into a *different* production assembly it never declared)
    // was tried and dropped: bare-identifier matching can't tell a type reference from a same-named
    // property (Assets/Level5/Core/Match/GameModeCompatibility.cs's `public GameModeCatalog Modes =>
    // modes;` false-positived against the unrelated `Modes` type in Level5.Constants). Nothing today
    // needs this guard - no production assembly references another yet - so it wasn't worth chasing
    // false positives to keep. Revisit if/when that stops being true.

    /// <summary>
    /// AUD-012 Phase 2b: proves production basketball types actually compile into the
    /// <c>Level5.Basketball</c> asmdef created for this slice, rather than falling back into
    /// <c>Assembly-CSharp</c> because of a misconfigured asmdef scope. Complements
    /// <see cref="NoMigratedProductionAssemblyReachesIntoAssemblyCSharp"/>, which checks the outbound
    /// edge (basketball source doesn't reach into Assembly-CSharp); this checks the assembly the
    /// compiler actually produced. This file has no asmdef of its own (see the class summary), so it
    /// compiles into Assembly-CSharp-Editor, which auto-references every autoReferenced runtime
    /// assembly - including Level5.Basketball - letting these types be referenced directly.
    /// </summary>
    [Test]
    public void BasketballProductionTypesCompileIntoLevel5Basketball()
    {
        const string expected = "Level5.Basketball";
        Assert.That(typeof(BasketBall).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(GameStats).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(BasketBallShotMarker).Assembly.GetName().Name, Is.EqualTo(expected));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 12: proves <c>MatchController</c> actually compiles into the
    /// <c>Level5.Match</c> asmdef created for this slice, the same identity check
    /// <see cref="BasketballProductionTypesCompileIntoLevel5Basketball"/> does for basketball.
    /// </summary>
    [Test]
    public void MatchControllerCompilesIntoLevel5Match()
    {
        Assert.That(
            typeof(MatchController).Assembly.GetName().Name,
            Is.EqualTo("Level5.Match"));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 13: proves the versus catalog types actually compile into the
    /// <c>Level5.Versus</c> asmdef created for this slice, the same identity check
    /// <see cref="MatchControllerCompilesIntoLevel5Match"/> does for the match leaf.
    /// </summary>
    [Test]
    public void VersusCatalogTypesCompileIntoLevel5Versus()
    {
        const string expected = "Level5.Versus";

        Assert.That(typeof(VersusCatalogs).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(DefaultCompetitiveRulesets).Assembly.GetName().Name, Is.EqualTo(expected));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 14: proves <c>AtomicFile</c> (extracted out of
    /// <c>CharacterProgressStore.cs</c>) actually compiles into the existing <c>Level5.Utility</c>
    /// asmdef, the same identity check <see cref="MatchControllerCompilesIntoLevel5Match"/> does for
    /// the match leaf.
    /// </summary>
    [Test]
    public void AtomicFileCompilesIntoLevel5Utility()
    {
        Assert.That(
            typeof(AtomicFile).Assembly.GetName().Name,
            Is.EqualTo("Level5.Utility"));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 15: proves the file-backed versus repository and the versus
    /// composition root actually compile into <c>Level5.Versus</c>, the same identity check
    /// <see cref="MatchControllerCompilesIntoLevel5Match"/> does for the match leaf.
    /// </summary>
    [Test]
    public void VersusRuntimeTypesCompileIntoLevel5Versus()
    {
        const string expected = "Level5.Versus";

        Assert.That(typeof(FileVersusSeriesRepository).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(VersusRuntime).Assembly.GetName().Name, Is.EqualTo(expected));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 16: proves the gameplay-to-versus result mapper actually compiles into
    /// <c>Level5.Versus</c>, the same identity check <see cref="MatchControllerCompilesIntoLevel5Match"/>
    /// does for the match leaf.
    /// </summary>
    [Test]
    public void GameStatsAttemptResultsCompilesIntoLevel5Versus()
    {
        Assert.That(
            typeof(GameStatsAttemptResults).Assembly.GetName().Name,
            Is.EqualTo("Level5.Versus"));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 17: proves <c>MatchSession</c> (the authoritative owner of the current
    /// match result id, moved out of <c>menu_progression</c>) actually compiles into <c>Level5.Match</c>,
    /// the same identity check <see cref="MatchControllerCompilesIntoLevel5Match"/> does for the
    /// match leaf.
    /// </summary>
    [Test]
    public void MatchSessionCompilesIntoLevel5Match()
    {
        Assert.That(
            typeof(MatchSession).Assembly.GetName().Name,
            Is.EqualTo("Level5.Match"));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 18: proves <c>ActiveMatch</c> (the authoritative current-match
    /// configuration, moved out of <c>menu_start</c>) actually compiles into <c>Level5.Match</c>,
    /// the same identity check <see cref="MatchSessionCompilesIntoLevel5Match"/> does for
    /// <c>MatchSession</c>.
    /// </summary>
    [Test]
    public void ActiveMatchCompilesIntoLevel5Match()
    {
        Assert.That(
            typeof(ActiveMatch).Assembly.GetName().Name,
            Is.EqualTo("Level5.Match"));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 19: proves <c>ActiveVersusAttempt</c> and <c>VersusMatchReporter</c>
    /// (the versus-side counterpart to <c>ActiveMatch</c>, and its only remaining
    /// <c>Assembly-CSharp</c> consumer) actually compile into <c>Level5.Versus</c>, the same identity
    /// check <see cref="ActiveMatchCompilesIntoLevel5Match"/> does for <c>ActiveMatch</c>.
    /// </summary>
    [Test]
    public void VersusMatchStateTypesCompileIntoLevel5Versus()
    {
        const string expected = "Level5.Versus";

        Assert.That(
            typeof(ActiveVersusAttempt).Assembly.GetName().Name,
            Is.EqualTo(expected));

        Assert.That(
            typeof(VersusMatchReporter).Assembly.GetName().Name,
            Is.EqualTo(expected));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 20: proves <c>MatchCatalogs</c> (split from its legacy-menu-prefab
    /// composition, which stays behind <c>LegacyMatchCatalogBootstrap</c> in <c>Assembly-CSharp</c>)
    /// actually compiles into <c>Level5.Match</c>, the same identity check
    /// <see cref="ActiveMatchCompilesIntoLevel5Match"/> does for <c>ActiveMatch</c>.
    /// </summary>
    [Test]
    public void MatchCatalogsCompilesIntoLevel5Match()
    {
        Assert.That(
            typeof(MatchCatalogs).Assembly.GetName().Name,
            Is.EqualTo("Level5.Match"));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 22: proves <c>RigidbodyFreezeHelper</c> (a plain static helper over
    /// <c>Rigidbody</c> constraints, moved out of loose <c>Assets/Scripts/Utility</c>) actually
    /// compiles into the existing <c>Level5.Utility</c> asmdef, the same identity check
    /// <see cref="AtomicFileCompilesIntoLevel5Utility"/> does for <c>AtomicFile</c>. Its three
    /// callers - <c>PlayerController</c>, <c>AutoPlayerController</c> and
    /// <c>RacingVehicleController</c> - stay in <c>Assembly-CSharp</c> and reach it through
    /// <c>autoReferenced</c>.
    /// </summary>
    [Test]
    public void RigidbodyFreezeHelperCompilesIntoLevel5Utility()
    {
        Assert.That(
            typeof(RigidbodyFreezeHelper).Assembly.GetName().Name,
            Is.EqualTo("Level5.Utility"));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 23: proves <c>PlayerSwapAttack</c> - the dependency-closed
    /// <c>MonoBehaviour</c> that establishes <c>Level5.Player</c> - actually compiles into the new
    /// asmdef, the same identity check <see cref="MatchControllerCompilesIntoLevel5Match"/> does for
    /// the match leaf. The asmdef deliberately sits in the <c>Level5Player/</c> subfolder rather than
    /// <c>Assets/Scripts/player/</c>, because asmdef ownership is recursive and the player root still
    /// holds <c>PlayerController</c> and other types that are not dependency-closed yet. Its four live
    /// consumers - <c>PlayerController</c>, <c>AutoPlayerController</c>, <c>BodyGuardController</c> and
    /// <c>EnemyController</c> - stay in <c>Assembly-CSharp</c> and reach it through
    /// <c>autoReferenced</c>.
    /// </summary>
    [Test]
    public void PlayerSwapAttackCompilesIntoLevel5Player()
    {
        Assert.That(
            typeof(PlayerSwapAttack).Assembly.GetName().Name,
            Is.EqualTo("Level5.Player"));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 24: proves <c>CallBallToPlayer</c> - the second type to enter
    /// <c>Level5.Player</c>, once its <c>MatchRuntime.Rules</c> read was inverted into an explicitly
    /// bound <c>ResolvedMatchRules</c> - actually compiles into that asmdef, the same identity check
    /// <see cref="PlayerSwapAttackCompilesIntoLevel5Player"/> does for the assembly's first type. Its
    /// callers - <c>PlayerController</c>, <c>AutoPlayerController</c> and <c>groundcheck</c> - stay in
    /// <c>Assembly-CSharp</c> and reach it through <c>autoReferenced</c>; <c>PlayerDunk</c> itself
    /// later moved into <c>Level5.Player</c> (Slice 36) - see
    /// <see cref="PlayerDunkCompilesIntoLevel5Player"/>.
    /// </summary>
    [Test]
    public void CallBallToPlayerCompilesIntoLevel5Player()
    {
        Assert.That(
            typeof(CallBallToPlayer).Assembly.GetName().Name,
            Is.EqualTo("Level5.Player"));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 25: proves <c>PlayerHealth</c> - the third type to enter
    /// <c>Level5.Player</c>, once its <c>MatchRuntime.Rules</c> read was inverted into an explicitly
    /// bound <c>ResolvedMatchRules</c> - actually compiles into that asmdef, the same identity check
    /// <see cref="CallBallToPlayerCompilesIntoLevel5Player"/> does for the assembly's second type. It
    /// is also the first type in this assembly to need <c>Level5.Combat</c>, for the
    /// <c>IDamageable</c>/<c>DamageInfo</c> contract it shares with other actors. Its consumers -
    /// <c>PlayerController</c>, <c>AutoPlayerController</c>, <c>PlayerCollisions</c>,
    /// <c>AutoPlayerCollisions</c>, <c>GameLevelManager</c>, <c>GameRules</c> and
    /// <c>EnemyController</c> - stay in <c>Assembly-CSharp</c> and reach it through
    /// <c>autoReferenced</c>.
    /// </summary>
    [Test]
    public void PlayerHealthCompilesIntoLevel5Player()
    {
        Assert.That(
            typeof(PlayerHealth).Assembly.GetName().Name,
            Is.EqualTo("Level5.Player"));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 26: proves <c>PlayerInputReader</c> - the human gameplay input-intent
    /// layer - actually compiles into the existing <c>Level5.Input</c> asmdef, the same identity check
    /// <see cref="PlayerHealthCompilesIntoLevel5Player"/> does for <c>PlayerHealth</c>. It joins
    /// <c>PlayerControls</c>, <c>PlayerControlsProvider</c> and <c>PlayerTouchInputState</c> there once
    /// its two <c>Assembly-CSharp</c> edges were cut: the <c>GameLevelManager.instance.Joystick</c> read
    /// became an explicitly composed <c>Func&lt;Vector2&gt;</c>, and the
    /// <c>TouchInputController.instance.HoldDetected</c> read was dropped as a duplicate of
    /// <c>PlayerTouchInputState.BlockHeld</c>. The asmdef needed no new reference - the final source
    /// names only <c>System</c>, <c>UnityEngine</c>, <c>PlayerControls</c> and
    /// <c>PlayerTouchInputState</c>. Its one consumer, <c>PlayerController</c>, stays in
    /// <c>Assembly-CSharp</c> and reaches it through <c>autoReferenced</c>.
    /// </summary>
    [Test]
    public void PlayerInputReaderCompilesIntoLevel5Input()
    {
        Assert.That(
            typeof(PlayerInputReader).Assembly.GetName().Name,
            Is.EqualTo("Level5.Input"));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 28: proves <c>CharacterProfile</c>, <c>CharacterProfileStatMapper</c>,
    /// <c>RuntimeCharacterStats</c> and <c>CharacterStats</c> - moved together because the mapper's
    /// write to <c>CharacterProfile.IsLocked</c> (<c>internal set</c>) only compiles from the same
    /// assembly - actually compile into <c>Level5.Player</c>, the same identity check
    /// <see cref="PlayerInputReaderCompilesIntoLevel5Input"/> does for <c>PlayerInputReader</c>.
    /// </summary>
    [Test]
    public void CharacterProfileOwnershipTypesCompileIntoLevel5Player()
    {
        const string expected = "Level5.Player";

        Assert.That(typeof(CharacterProfile).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(CharacterProfileStatMapper).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(RuntimeCharacterStats).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(CharacterStats).Assembly.GetName().Name, Is.EqualTo(expected));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 29: proves <c>ShooterAttributesMapper</c> - a pure assembly-ownership
    /// move, dependency-closed since Slice 28 moved <c>CharacterProfile</c> into <c>Level5.Player</c> -
    /// actually compiles into that asmdef, the same identity check
    /// <see cref="CharacterProfileOwnershipTypesCompileIntoLevel5Player"/> does for
    /// <c>CharacterProfile</c>. Its callers - <c>PlayerController</c> and <c>AutoPlayerController</c> -
    /// stay in <c>Assembly-CSharp</c> and reach it through <c>autoReferenced</c>.
    /// </summary>
    [Test]
    public void ShooterAttributesMapperCompilesIntoLevel5Player()
    {
        Assert.That(
            typeof(ShooterAttributesMapper).Assembly.GetName().Name,
            Is.EqualTo("Level5.Player"));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 30: proves <c>EnemyPopulationRules</c> - the shared pure spawn/queue
    /// capacity policy moved out of <c>Assets/Scripts/enemy/</c>, unchanged, into
    /// <c>Assets/Level5/Core/Match/</c> - actually compiles into <c>Level5.Core</c>, the same identity
    /// check <see cref="MatchControllerCompilesIntoLevel5Match"/> does for the match leaf. Its
    /// consumers - <c>EnemySpawner</c> and <c>PlayerAttackQueue</c> - stay in <c>Assembly-CSharp</c> and
    /// reach it through <c>autoReferenced</c>, exactly as they already did before the move.
    /// </summary>
    [Test]
    public void EnemyPopulationRulesCompilesIntoLevel5Core()
    {
        Assert.That(
            typeof(EnemyPopulationRules).Assembly.GetName().Name,
            Is.EqualTo("Level5.Core"));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 31: proves <c>PlayerAttackQueue</c> and <c>PlayerAttackPosition</c> -
    /// moved together because the position slot's <c>Initialize(PlayerAttackQueue owner, int slotId)</c>
    /// parameter only compiles from the same assembly as the queue - actually compile into
    /// <c>Level5.Player</c>, the same identity check <see cref="CharacterProfileOwnershipTypesCompileIntoLevel5Player"/>
    /// does for <c>CharacterProfile</c>. Slice 30 already cut the queue's <c>MatchRuntime</c> and
    /// <c>PlayerIdentifier</c> dependencies (still enforced by
    /// <see cref="Level5PlayerAttackQueueDependencyGuardTests"/>); this is the pure ownership move
    /// that follows.
    /// </summary>
    [Test]
    public void PlayerAttackQueueTypesCompileIntoLevel5Player()
    {
        const string expected = "Level5.Player";

        Assert.That(typeof(PlayerAttackQueue).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(PlayerAttackPosition).Assembly.GetName().Name, Is.EqualTo(expected));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 32: proves <c>PlayerDamageReactions</c> and its new
    /// <c>IPlayerDamageReactionHost</c> contract - the human damage/knockdown/lightning/shrink
    /// reaction helper, dependency-closed by inverting its <c>PlayerController</c> and
    /// <c>CameraManager</c> reads into the host contract and a composed <c>Func&lt;Camera&gt;</c> -
    /// actually compile into <c>Level5.Player</c>, the same identity check
    /// <see cref="PlayerAttackQueueTypesCompileIntoLevel5Player"/> does for <c>PlayerAttackQueue</c>.
    /// </summary>
    [Test]
    public void PlayerDamageReactionTypesCompileIntoLevel5Player()
    {
        const string expected = "Level5.Player";

        Assert.That(typeof(PlayerDamageReactions).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(IPlayerDamageReactionHost).Assembly.GetName().Name, Is.EqualTo(expected));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 33: proves <c>IPlayerIdleSniperRuntime</c> - the narrow contract
    /// <c>PlayerController</c>'s idle-sniper check uses instead of the concrete <c>SniperManager</c>
    /// (still <c>Assembly-CSharp</c>) - actually compiles into <c>Level5.Player</c>, the same identity
    /// check <see cref="PlayerDamageReactionTypesCompileIntoLevel5Player"/> does for
    /// <c>IPlayerDamageReactionHost</c>.
    /// </summary>
    [Test]
    public void PlayerIdleSniperRuntimeCompilesIntoLevel5Player()
    {
        Assert.That(
            typeof(IPlayerIdleSniperRuntime).Assembly.GetName().Name,
            Is.EqualTo("Level5.Player"));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 34: proves <c>IPlayerControllerParticipantState</c> - the narrow
    /// contract <c>PlayerController</c> resolves instead of the concrete <c>PlayerIdentifier</c> (still
    /// <c>Assembly-CSharp</c>) - actually compiles into <c>Level5.Player</c>, the same identity check
    /// <see cref="PlayerIdleSniperRuntimeCompilesIntoLevel5Player"/> does for
    /// <c>IPlayerIdleSniperRuntime</c>.
    /// </summary>
    [Test]
    public void PlayerControllerParticipantStateCompilesIntoLevel5Player()
    {
        Assert.That(
            typeof(IPlayerControllerParticipantState).Assembly.GetName().Name,
            Is.EqualTo("Level5.Player"));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 35: proves <c>IPlayerDunkHost</c> - the narrow contract <c>PlayerDunk</c>
    /// resolves instead of the concrete <c>PlayerController</c> (still <c>Assembly-CSharp</c>) - actually
    /// compiles into <c>Level5.Player</c>, the same identity check
    /// <see cref="PlayerControllerParticipantStateCompilesIntoLevel5Player"/> does for
    /// <c>IPlayerControllerParticipantState</c>. <c>PlayerDunk</c> itself is not moved this slice - see
    /// <see cref="Level5PlayerDunkDependencyGuardTests"/> for the dependency-cut it now satisfies.
    /// </summary>
    [Test]
    public void PlayerDunkHostCompilesIntoLevel5Player()
    {
        Assert.That(
            typeof(IPlayerDunkHost).Assembly.GetName().Name,
            Is.EqualTo("Level5.Player"));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 36: proves <c>PlayerDunk</c> itself - dependency-closed by Slice 35,
    /// then moved source-identically - actually compiles into <c>Level5.Player</c>, the same identity
    /// check <see cref="PlayerDunkHostCompilesIntoLevel5Player"/> does for <c>IPlayerDunkHost</c>. Its
    /// one live consumer, <c>PlayerController</c>, stays in <c>Assembly-CSharp</c> and reaches it
    /// through <c>autoReferenced</c>.
    /// </summary>
    [Test]
    public void PlayerDunkCompilesIntoLevel5Player()
    {
        Assert.That(
            typeof(PlayerDunk).Assembly.GetName().Name,
            Is.EqualTo("Level5.Player"));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 37: proves <c>IPlayerMatchRuntime</c> - the narrow contract
    /// <c>PlayerController</c> resolves instead of the static <c>MatchRuntime</c> (still
    /// <c>Assembly-CSharp</c>), its last direct dependency on that assembly - actually compiles into
    /// <c>Level5.Player</c>, the same identity check <see cref="PlayerDunkHostCompilesIntoLevel5Player"/>
    /// does for <c>IPlayerDunkHost</c>.
    /// </summary>
    [Test]
    public void PlayerMatchRuntimeCompilesIntoLevel5Player()
    {
        Assert.That(
            typeof(IPlayerMatchRuntime).Assembly.GetName().Name,
            Is.EqualTo("Level5.Player"));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 38: proves <c>PlayerController</c> - dependency-closed by Slice 37, then
    /// moved source-identically - actually compiles into <c>Level5.Player</c>, the same identity check
    /// <see cref="PlayerDunkCompilesIntoLevel5Player"/> does for <c>PlayerDunk</c>.
    /// </summary>
    [Test]
    public void PlayerControllerCompilesIntoLevel5Player()
    {
        Assert.That(
            typeof(PlayerController).Assembly.GetName().Name,
            Is.EqualTo("Level5.Player"));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 48: proves the character preset/progression cluster -
    /// <c>CharacterPreset</c>, <c>CharacterPresetCatalog</c>, <c>CharacterUpgradeLevels</c>,
    /// <c>PlayerCharacterProgress</c>, <c>CharacterProgressSave</c>, <c>CharacterProgressResolver</c>
    /// and <c>CharacterProgressStore</c> - actually compile into <c>Level5.Player</c>, the same
    /// identity check <see cref="CharacterProfileOwnershipTypesCompileIntoLevel5Player"/> does for
    /// <c>CharacterProfile</c>. Moved together as one dependency-closed cluster (catalog/save/progress
    /// models all reference each other); <c>CharacterRuntimeProvider</c> and
    /// <c>CharacterProgressAccountId</c> stay in <c>Assembly-CSharp</c> and reach it through
    /// <c>autoReferenced</c>.
    /// </summary>
    [Test]
    public void CharacterPresetProgressionTypesCompileIntoLevel5Player()
    {
        const string expected = "Level5.Player";

        Assert.That(typeof(CharacterPreset).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(CharacterPresetCatalog).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(CharacterUpgradeLevels).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(PlayerCharacterProgress).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(CharacterProgressSave).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(CharacterProgressResolver).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(CharacterProgressStore).Assembly.GetName().Name, Is.EqualTo(expected));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 49: proves <c>PlayerAttackBox</c> - a pure assembly-ownership move,
    /// already dependency-closed on <c>UnityEngine</c> only - actually compiles into
    /// <c>Level5.Player</c>, the same identity check <see cref="CharacterPresetProgressionTypesCompileIntoLevel5Player"/>
    /// does for the character preset/progression cluster. Its consumers - <c>PlayerCollisions</c>,
    /// <c>AutoPlayerCollisions</c> and <c>EnemyCollisions</c> - stay in <c>Assembly-CSharp</c> and reach
    /// it through <c>autoReferenced</c>.
    /// </summary>
    [Test]
    public void PlayerAttackBoxCompilesIntoLevel5Player()
    {
        Assert.That(
            typeof(PlayerAttackBox).Assembly.GetName().Name,
            Is.EqualTo("Level5.Player"));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 50: proves the remaining 15 named player-domain targets - the CPU
    /// actor/defense cluster, the account/character-runtime pair, the participant/ground/collision
    /// cluster and the presentation/animation cluster - actually compile into <c>Level5.Player</c>,
    /// the same identity check <see cref="PlayerAttackBoxCompilesIntoLevel5Player"/> does for
    /// <c>PlayerAttackBox</c>. Dependency cuts: <c>CharacterProgressAccountId</c> now reads
    /// <c>Level5.Core.LocalAccountIdentity</c> instead of <c>GameOptions.userid</c>/<c>userName</c>;
    /// <c>AutoPlayerController</c>/<c>AutoPlayerDefense</c> take arena context, match runtime and the
    /// moved <c>PlayerRegistry</c> through explicit composition instead of
    /// <c>GameLevelManager.instance</c>/<c>MatchRuntime</c>; <c>PlayerCollisions</c>/
    /// <c>AutoPlayerCollisions</c> read attack-box metadata through the new
    /// <c>IAttackBoxHitInfo</c> (<c>Level5.Combat</c>) instead of naming <c>EnemyAttackBox</c>
    /// (<c>Level5.Enemy</c>) directly; <c>PlayerHealthBar</c> takes its tracked participant through
    /// <c>GameLevelManager.BindPlayerHealthBarContext</c>; <c>PlayerAnimationEvents</c> takes a
    /// projectile-spawn delegate (<c>ProjectilePool</c> is <c>Assembly-CSharp</c>, not
    /// <c>Level5.Pooling</c>) and a live auto-player reader instead of
    /// <c>ProjectilePool.Spawn</c>/<c>GameLevelManager.instance.AutoPlayer</c>;
    /// <c>CheerleaderSwapAnimation</c> reads <c>PlayerControlsProvider.Controls</c> (the same
    /// authoritative <c>Level5.Input</c> object <c>GameLevelManager.Controls</c> already forwarded)
    /// instead of <c>GameLevelManager.instance.Controls</c>.
    /// </summary>
    [Test]
    public void RemainingPlayerDomainTargetsCompileIntoLevel5Player()
    {
        const string expected = "Level5.Player";

        Assert.That(typeof(CharacterProgressAccountId).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(CharacterRuntimeProvider).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(AutoPlayerController).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(AutoPlayerDamageReactions).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(AutoPlayerDefense).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(CollisionCheckDefense).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(GroundCheckDefense).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(CpuScoreDeficit).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(PlayerIdentifier).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(GroundCheck).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(PlayerCollisions).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(AutoPlayerCollisions).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(PlayerAnimationEvents).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(CheerleaderSwapAnimation).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(PlayerHealthBar).Assembly.GetName().Name, Is.EqualTo(expected));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 50: proves <c>PlayerRegistry</c> - supporting work moved alongside the
    /// 15 named targets because it became dependency-closed the moment <c>PlayerIdentifier</c> moved,
    /// and doing so let <c>AutoPlayerController</c>/<c>AutoPlayerDefense</c> compose one participant
    /// seam instead of two - actually compiles into <c>Level5.Player</c>, the same identity check
    /// <see cref="RemainingPlayerDomainTargetsCompileIntoLevel5Player"/> does for its 15 siblings. Its
    /// callers - <c>GameLevelManager</c> and <c>SpawnCoordinator</c> - stay in <c>Assembly-CSharp</c>
    /// and reach it through <c>autoReferenced</c>.
    /// </summary>
    [Test]
    public void PlayerRegistryCompilesIntoLevel5Player()
    {
        Assert.That(
            typeof(PlayerRegistry).Assembly.GetName().Name,
            Is.EqualTo("Level5.Player"));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 51: proves <c>PlayerStats</c> and <c>SelectedLoadout</c> - the last two
    /// loose player-domain production types, deliberately left behind by Slice 50 as "non-target
    /// trivial leaves" - actually compile into <c>Level5.Player</c>, the same identity check
    /// <see cref="RemainingPlayerDomainTargetsCompileIntoLevel5Player"/> does for its 15 siblings. Pure
    /// ownership moves: <c>PlayerStats</c> stays a live compatibility <c>MonoBehaviour</c> for old
    /// scenes/prefabs that still serialize its MonoScript GUID (<c>a215fc91b23f9574fa89bf961cf9a25a</c>,
    /// unchanged by the move); <c>SelectedLoadout</c> keeps its GUID
    /// (<c>a64efc12b3c5482cb504cb0e6b557221</c>) too. This closes the player-root ownership boundary -
    /// see <see cref="NoProductionSourceExistsDirectlyUnderThePlayerRootOutsideLevel5Player"/> for the
    /// permanent invariant that follows from it.
    /// </summary>
    [Test]
    public void FinalPlayerRootTypesCompileIntoLevel5Player()
    {
        const string expected = "Level5.Player";

        Assert.That(typeof(PlayerStats).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(SelectedLoadout).Assembly.GetName().Name, Is.EqualTo(expected));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 51: the player-root ownership boundary this slice completes - every
    /// production <c>.cs</c> beneath <c>Assets/Scripts/player/</c> is now owned by
    /// <c>Assets/Scripts/player/Level5Player/</c>, with no loose file left directly under the player
    /// root (<see cref="FinalPlayerRootTypesCompileIntoLevel5Player"/> moved the last two). Reuses
    /// <see cref="EnumerateFilesUnder"/> the same way every other structural guard in this fixture
    /// does, scoped to only the player root. The invariant intentionally describes the current
    /// architecture, not a configurable repository-wide path-policy system - a future deliberate
    /// player subassembly must update this guard explicitly.
    /// </summary>
    [Test]
    public void NoProductionSourceExistsDirectlyUnderThePlayerRootOutsideLevel5Player()
    {
        string playerRoot = Path.Combine(ScriptsRoot, "player");
        string level5PlayerPrefix = Path.Combine(playerRoot, "Level5Player").Replace('\\', '/') + "/";

        List<string> offenders = EnumerateFilesUnder(new[] { playerRoot })
            .Where(file => !file.Replace('\\', '/')
                .StartsWith(level5PlayerPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(Level5TestSourceText.Relative)
            .ToList();

        Assert.That(
            offenders,
            Is.Empty,
            "every production .cs beneath Assets/Scripts/player/ must be owned by "
                + "Assets/Scripts/player/Level5Player/ - found loose file(s) directly under the player "
                + "root:\n" + string.Join("\n", offenders));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 50: proves the new neutral <c>IAttackBoxHitInfo</c> contract - added so
    /// <c>PlayerCollisions</c>/<c>AutoPlayerCollisions</c> (<c>Level5.Player</c>) can read either
    /// <c>PlayerAttackBox</c> or <c>EnemyAttackBox</c> (<c>Level5.Enemy</c>) without a Player&lt;-&gt;Enemy
    /// assembly cycle - actually compiles into the existing <c>Level5.Combat</c> asmdef, the same
    /// identity check <see cref="PlayerRegistryCompilesIntoLevel5Player"/> does for
    /// <c>PlayerRegistry</c>. Implemented by both attack-box types; <c>Level5.Enemy</c> gained a new
    /// reference to <c>Level5.Combat</c> (a leaf with no references of its own) to implement it.
    /// </summary>
    [Test]
    public void AttackBoxHitInfoCompilesIntoLevel5Combat()
    {
        Assert.That(
            typeof(IAttackBoxHitInfo).Assembly.GetName().Name,
            Is.EqualTo("Level5.Combat"));
    }

    /// <summary>
    /// AUD-012 Phase 2b, Slice 52: proves <c>MatchRuntime</c> - dependency-closed by replacing its
    /// direct <c>GameOptions</c>/<c>Modes</c> reads with a <c>LegacyMatchRuntimeSnapshot</c> resolved
    /// through one installable reader (<c>GameOptions.CaptureMatchRuntimeSnapshot</c>, still
    /// <c>Assembly-CSharp</c>, installed by that file's own <c>RuntimeInitializeOnLoadMethod</c>
    /// bootstrap) - actually compiles into <c>Level5.Match</c>, the same identity check
    /// <see cref="ActiveMatchCompilesIntoLevel5Match"/> does for <c>ActiveMatch</c>.
    /// </summary>
    [Test]
    public void MatchRuntimeCompilesIntoLevel5Match()
    {
        Assert.That(
            typeof(MatchRuntime).Assembly.GetName().Name,
            Is.EqualTo("Level5.Match"));
    }

    [Test]
    public void NoProductionAssemblyReferencesAKnownEditorOnlyPackageAssembly()
    {
        List<string> offenders = new List<string>();
        foreach (AsmdefInfo assembly in ProductionAssemblies.Value)
        {
            foreach (string editorOnly in EditorOnlyPackageAssemblies)
            {
                if (assembly.References.Contains(editorOnly))
                {
                    offenders.Add($"{Level5TestSourceText.Relative(assembly.Path)} references \"{editorOnly}\"");
                }
            }
        }

        Assert.That(offenders, Is.Empty, string.Join("\n", offenders));
    }

    /// <summary>
    /// AUD-012 Phase 2d: PlayMode gameplay tests must declare proper test assembly dependencies rather
    /// than silently returning to the asmdef-free <c>Assets/Tests/PlayModeGameplay</c> workaround Phase
    /// 2c removed (Slice 47, the folder's last file, <c>GameplayLevelUnpauseTests</c>, migrated into
    /// <c>Level5.PlayModeTests</c>). Reads the directory directly rather than caching whether it existed
    /// at discovery time, so the guard stays effective even if the folder is recreated later.
    /// </summary>
    [Test]
    public void NoSourceFileReturnsToTheAsmdefFreePlayModeGameplayWorkaround()
    {
        string workaroundFolder = Path.Combine(AssetsRoot, "Tests", "PlayModeGameplay");
        if (!Directory.Exists(workaroundFolder))
        {
            return;
        }

        List<string> offenders = EnumerateFilesUnder(new[] { workaroundFolder })
            .Select(Level5TestSourceText.Relative)
            .ToList();

        Assert.That(
            offenders,
            Is.Empty,
            "PlayMode gameplay tests must declare proper test assembly dependencies rather than "
                + "silently returning to the asmdef-free Assets/Tests/PlayModeGameplay workaround:\n"
                + string.Join("\n", offenders));
    }

    /// <summary>One discovered production (non-test) asmdef: its name, declared references, and folder.</summary>
    private sealed class AsmdefInfo
    {
        public string Path;
        public string Folder;
        public string Name;
        public HashSet<string> References;
    }

    /// <summary>Mirrors the subset of an .asmdef's JSON this file reads, for <see cref="JsonUtility"/>.</summary>
    [Serializable]
    private class AsmdefJson
    {
        public string name;
        public string[] references;
        public string[] optionalUnityReferences;
    }

    private static List<AsmdefInfo> DiscoverProductionAssemblies()
    {
        List<string> found = new List<string>();
        if (Directory.Exists(ScriptsRoot))
        {
            found.AddRange(Directory.EnumerateFiles(ScriptsRoot, "*.asmdef", SearchOption.AllDirectories));
        }

        if (Directory.Exists(Level5Root))
        {
            found.AddRange(Directory.EnumerateFiles(Level5Root, "*.asmdef", SearchOption.AllDirectories));
        }

        List<AsmdefInfo> assemblies = new List<AsmdefInfo>();
        foreach (string path in found)
        {
            if (path.Replace('\\', '/').Contains("~"))
            {
                continue;
            }

            AsmdefJson json = JsonUtility.FromJson<AsmdefJson>(File.ReadAllText(path));

            // Test assemblies opt in via optionalUnityReferences: ["TestAssemblies"].
            if (json?.optionalUnityReferences != null && json.optionalUnityReferences.Contains("TestAssemblies"))
            {
                continue;
            }

            assemblies.Add(new AsmdefInfo
            {
                Path = path,
                Folder = Path.GetDirectoryName(path),
                Name = json?.name ?? Path.GetFileNameWithoutExtension(path),
                References = new HashSet<string>(json?.references ?? Array.Empty<string>()),
            });
        }

        return assemblies;
    }

    private static IEnumerable<string> EnumerateFilesUnder(IEnumerable<string> folders)
    {
        foreach (string folder in folders)
        {
            foreach (string file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
            {
                if (!file.Replace('\\', '/').Contains("~"))
                {
                    yield return file;
                }
            }
        }
    }

    /// <summary>
    /// Scans every <c>.cs</c> file anywhere under <c>Assets/</c> that isn't owned by some asmdef
    /// (production, test, or third-party - Unity draws no distinction) and isn't under an
    /// <c>Editor</c> or <c>Tests</c> path segment (those compile into a different predefined
    /// assembly than runtime Assembly-CSharp). Earlier versions of this scan only walked
    /// <c>Assets/Scripts</c> plus loose files directly under <c>Assets/</c>, missing vendored
    /// third-party folders with no asmdef of their own (<c>Assets/Standard Assets</c>,
    /// <c>Assets/Joystick Pack</c>, <c>Assets/OmniSARTechnologies</c> - 50 files, all genuinely part
    /// of Assembly-CSharp) - a false-negative gap found by code review, 2026-08-21.
    /// </summary>
    private static IEnumerable<string> EnumerateAssemblyCSharpScripts()
    {
        if (!Directory.Exists(AssetsRoot))
        {
            yield break;
        }

        HashSet<string> asmdefOwnedFolders = new HashSet<string>(
            Directory.EnumerateFiles(AssetsRoot, "*.asmdef", SearchOption.AllDirectories)
                .Where(path => !path.Replace('\\', '/').Contains("~"))
                .Select(path => Path.GetDirectoryName(path).Replace('\\', '/')),
            StringComparer.OrdinalIgnoreCase);

        foreach (string file in Directory.EnumerateFiles(AssetsRoot, "*.cs", SearchOption.AllDirectories))
        {
            string normalized = file.Replace('\\', '/');
            if (normalized.Contains("~"))
            {
                continue;
            }

            if (HasPathSegment(normalized, "Editor") || HasPathSegment(normalized, "Tests"))
            {
                continue;
            }

            bool ownedByAnAsmdef = asmdefOwnedFolders.Any(folder =>
                normalized.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase));
            if (!ownedByAnAsmdef)
            {
                yield return file;
            }
        }
    }

    private static bool HasPathSegment(string normalizedPath, string segment)
    {
        return normalizedPath.Split('/').Any(part => string.Equals(part, segment, StringComparison.OrdinalIgnoreCase));
    }

    private static HashSet<string> UsedIdentifiers(string file)
    {
        string text = NormalizeSource(File.ReadAllText(file));
        return new HashSet<string>(Regex.Matches(text, @"\b[A-Za-z_][A-Za-z0-9_]*\b").Select(m => m.Value));
    }

    /// <summary>
    /// Only <c>public</c> declarations not nested inside another type count: a type nested inside a
    /// class/struct/interface can't be named by a bare identifier from another assembly at all -
    /// matching on those produced pure name collisions (a private nested <c>StatsManager.mode</c>
    /// class was colliding with every unrelated local variable named <c>mode</c> across the
    /// codebase). Nesting inside a <c>namespace</c> block does not exclude a type the same way -
    /// this project's identifier-based matching doesn't distinguish "needs a using directive" from
    /// "is directly in scope" either, so a namespace-scoped type is still a real, reachable name and
    /// belongs in the set (most of this codebase's Assembly-CSharp model/service types are declared
    /// this way, e.g. <c>HighScoreModel</c>, <c>UserModel</c>).
    /// </summary>
    private static HashSet<string> CollectDeclaredTypeNames(IEnumerable<string> files)
    {
        HashSet<string> types = new HashSet<string>(StringComparer.Ordinal);
        foreach (string file in files)
        {
            string text = NormalizeSource(File.ReadAllText(file));
            CollectTypesNotNestedInAnotherType(text, types);
        }

        return types;
    }

    /// <summary>
    /// Walks brace depth, tracking whether each open brace belongs to a type (class/struct/interface/
    /// enum) or something else (namespace, method body, block). A <c>public</c> type declaration is
    /// recorded only when no enclosing brace belongs to a type - checked in O(1) via
    /// <c>typeFrameCount</c> rather than re-scanning the stack on every brace.
    /// </summary>
    private static void CollectTypesNotNestedInAnotherType(string text, HashSet<string> types)
    {
        Regex token = new Regex(
            @"(?<decl>\bpublic\s+(?:sealed\s+|abstract\s+|static\s+|partial\s+)*"
            + @"(?:class|interface|struct|enum)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*))"
            + @"|(?<open>\{)|(?<close>\})",
            RegexOptions.Singleline);

        Stack<bool> enclosingIsType = new Stack<bool>();
        int typeFrameCount = 0;
        string pendingTypeName = null;

        foreach (Match match in token.Matches(text))
        {
            if (match.Groups["decl"].Success)
            {
                pendingTypeName = match.Groups["name"].Value;
            }
            else if (match.Groups["open"].Success)
            {
                bool opensType = pendingTypeName != null;
                if (opensType && typeFrameCount == 0)
                {
                    types.Add(pendingTypeName);
                }

                enclosingIsType.Push(opensType);
                if (opensType)
                {
                    typeFrameCount++;
                }

                pendingTypeName = null;
            }
            else if (match.Groups["close"].Success && enclosingIsType.Count > 0)
            {
                if (enclosingIsType.Pop())
                {
                    typeFrameCount--;
                }
            }
        }
    }

    /// <summary>
    /// Removes comments and string/char literals, so a table-name constant or a
    /// <c>[Tooltip("...")]</c> string that happens to spell a foreign type's name is not mistaken
    /// for a real reference to it, and a type named only in a doc comment is not either.
    ///
    /// This used to be two regex passes (literals, then comments). That ordering was chosen to keep
    /// a URL string's <c>//</c> from being read as a comment, but it made the char-literal rule run
    /// over not-yet-removed comment text, where an apostrophe in prose paired with the next
    /// apostrophe in the file and deleted everything between - blinding this guard to whole regions
    /// of 29 production files. <see cref="Level5TestSourceText.StripCommentsAndLiterals"/> replaces
    /// both passes with one left-to-right scan that has neither failure mode.
    /// </summary>
    private static string NormalizeSource(string text)
    {
        return Level5TestSourceText.StripCommentsAndLiterals(text);
    }
}
