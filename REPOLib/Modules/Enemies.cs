using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using JetBrains.Annotations;
using REPOLib.Extensions;
using UnityEngine;

namespace REPOLib.Modules;

/// <summary>
///     The Enemies module of REPOLib.
/// </summary>
[PublicAPI]
public static class Enemies
{
    internal static int SpawnNextEnemiesNotDespawned;
    private static readonly List<EnemySetup> _enemiesToRegister = [];
    private static readonly List<EnemySetup> _enemiesRegistered = [];
    private static bool _initialEnemiesRegistered;

    /// <inheritdoc cref="GetEnemies" />
    public static IReadOnlyList<EnemySetup> AllEnemies
        => GetEnemies();

    /// <summary>
    ///     Gets all enemies registered with REPOLib.
    /// </summary>
    public static IReadOnlyList<EnemySetup> RegisteredEnemies
        => _enemiesRegistered;

    internal static void RegisterInitialEnemies()
    {
        if (_initialEnemiesRegistered)
            return;

        _enemiesToRegister.ForEach(RegisterEnemyWithGame);
        _initialEnemiesRegistered = true;
        _enemiesToRegister.Clear();
    }

    private static void RegisterEnemyWithGame(EnemySetup enemy)
    {
        if (_enemiesRegistered.Contains(enemy))
            return;

        if (!enemy.TryGetEnemyParent(out EnemyParent? enemyParent))
            return;

        if (EnemyDirector.instance.AddEnemy(enemy))
        {
            _enemiesRegistered.Add(enemy);
            Logger.LogInfo($"Added enemy \"{enemyParent.enemyName}\" to difficulty {enemyParent.difficulty}", true);
            return;
        }

        Logger.LogWarning($"Failed to add enemy \"{enemyParent.enemyName}\" to difficulty {enemyParent.difficulty}", true);
    }

    /// <summary>
    ///     Registers an <see cref="EnemySetup" />.
    /// </summary>
    /// <param name="enemySetup">The <see cref="EnemySetup" /> to register.</param>
    /// <exception cref="ArgumentException"></exception>
    public static void RegisterEnemy(EnemySetup enemySetup)
    {
        if (enemySetup == null || enemySetup.spawnObjects == null || enemySetup.spawnObjects.Count == 0)
            throw new ArgumentException("Failed to register enemy. EnemySetup or spawnObjects list is empty.");

        EnemyParent? enemyParent = enemySetup.GetEnemyParent();
        if (enemyParent == null)
        {
            Logger.LogError($"Failed to register enemy \"{enemySetup.name}\". No enemy prefab found in spawnObjects list.");
            return;
        }

        if (ResourcesHelper.HasEnemyPrefab(enemySetup))
        {
            Logger.LogError($"Failed to register enemy \"{enemyParent.enemyName}\". Enemy prefab already exists in Resources with the same name.");
            return;
        }

        if (_enemiesToRegister.Contains(enemySetup))
        {
            Logger.LogError($"Failed to register enemy \"{enemyParent.enemyName}\". Enemy is already registered!");
            return;
        }

        foreach (GameObject? spawnObject in enemySetup.GetDistinctSpawnObjects())
        foreach (EnemySetup? previousEnemy in _enemiesToRegister.Where(previousEnemy =>
                     previousEnemy.AnySpawnObjectsNameEqualsAnother(spawnObject)))
        {
            Logger.LogError($"Failed to register enemy \"{enemyParent.enemyName}\". Enemy \"{previousEnemy.name}\" already has a spawn object called \"{spawnObject.name}\"");
            return;
        }

        // Register all spawn prefabs to the network
        foreach (GameObject? spawnObject in enemySetup.GetDistinctSpawnObjects())
        {
            string prefabId = ResourcesHelper.GetEnemyPrefabPath(spawnObject);
            if (!NetworkPrefabs.HasNetworkPrefab(prefabId)) NetworkPrefabs.RegisterNetworkPrefab(prefabId, spawnObject);

            Utilities.FixAudioMixerGroups(spawnObject);
        }

        if (_initialEnemiesRegistered)
            RegisterEnemyWithGame(enemySetup);
        else
            _enemiesToRegister.Add(enemySetup);
    }

    /// <summary>
    ///     Spawns an enemy or enemies from a <see cref="EnemySetup" />.
    /// </summary>
    /// <param name="enemySetup">The <see cref="EnemySetup" /> to spawn the enemy or enemies from.</param>
    /// <param name="position">The position where the enemy will be spawned.</param>
    /// <param name="rotation">The rotation of the enemy.</param>
    /// <param name="spawnDespawned">Whether this enemy will spawn despawned.</param>
    /// <returns>The <see cref="EnemyParent" /> objects from spawned enemies.</returns>
    public static List<EnemyParent>? SpawnEnemy(EnemySetup? enemySetup, Vector3 position, Quaternion rotation,
        bool spawnDespawned = true)
    {
        if (enemySetup == null)
        {
            Logger.LogError("Failed to spawn enemy. EnemySetup is null.");
            return null;
        }

        if (!enemySetup.TryGetEnemyParent(out EnemyParent? prefabEnemyParent))
        {
            Logger.LogError("Failed to spawn enemy. EnemyParent is null.");
            return null;
        }

        if (LevelGenerator.Instance == null)
        {
            Logger.LogError($"Failed to spawn enemy \"{prefabEnemyParent.enemyName}\". EnemySetup instance is null.");
            return null;
        }

        if (RunManager.instance == null)
        {
            Logger.LogError($"Failed to spawn enemy \"{prefabEnemyParent.enemyName}\". RunManager instance is null.");
            return null;
        }

        if (EnemyDirector.instance == null)
        {
            Logger.LogError($"Failed to spawn enemy \"{prefabEnemyParent.enemyName}\". EnemyDirector instance is null.");
            return null;
        }

        List<EnemyParent> enemyParents = [];
        foreach (GameObject? spawnObject in enemySetup.GetSortedSpawnObjects())
        {
            if (spawnObject == null)
            {
                Logger.LogError($"Failed to spawn enemy \"{prefabEnemyParent.enemyName}\" spawn object. GameObject is null.");
                continue;
            }

            string prefabId = ResourcesHelper.GetEnemyPrefabPath(spawnObject);
            GameObject? gameObject = NetworkPrefabs.SpawnNetworkPrefab(prefabId, position, rotation);

            if (gameObject == null)
            {
                Logger.LogError($"Failed to spawn enemy \"{prefabEnemyParent.enemyName}\" spawn object \"{spawnObject.name}\"");
                continue;
            }

            if (!gameObject.TryGetComponent(out EnemyParent enemyParent))
                continue;

            enemyParents.Add(enemyParent);
            if (!spawnDespawned)
                SpawnNextEnemiesNotDespawned++;

            enemyParent.SetupDone = true;
            Enemy? enemy = gameObject.GetComponentInChildren<Enemy>();

            if (enemy != null)
                enemy.EnemyTeleported(position);
            else
                Logger.LogError($"Enemy \"{prefabEnemyParent.enemyName}\" spawn object \"{spawnObject.name}\" does not have an enemy component.");

            LevelGenerator.Instance.EnemiesSpawnTarget++;
            EnemyDirector.instance.FirstSpawnPointAdd(enemyParent);
        }

        if (enemyParents.Count == 0)
        {
            Logger.LogInfo($"Failed to spawn enemy \"{prefabEnemyParent.enemyName}\". No spawn objects where spawned.");
            return enemyParents;
        }

        Logger.LogInfo($"Spawned enemy \"{prefabEnemyParent.enemyName}\" at position {position}", true);
        RunManager.instance.EnemiesSpawnedRemoveEnd();
        return enemyParents;
    }

    /// <summary>
    ///     Gets all <see cref="EnemySetup" /> objects.
    /// </summary>
    /// <returns>A list of all <see cref="EnemySetup" /> objects.</returns>
    public static IReadOnlyList<EnemySetup> GetEnemies()
        => EnemyDirector.instance == null ? [] : EnemyDirector.instance.GetEnemies();

    /// <summary>
    ///     Tries to get an <see cref="EnemySetup" /> by name or by its <see cref="EnemyParent" /> name.
    /// </summary>
    /// <param name="name">The <see cref="string" /> to match.</param>
    /// <param name="enemySetup">The found <see cref="EnemySetup" />.</param>
    /// <returns>Whether the <see cref="EnemySetup" /> was found.</returns>
    public static bool TryGetEnemyByName(string? name, [NotNullWhen(true)] out EnemySetup? enemySetup)
        => (enemySetup = GetEnemyByName(name)) != null;

    /// <summary>
    ///     Gets an <see cref="EnemySetup" /> by name or by its <see cref="EnemyParent" /> name.
    /// </summary>
    /// <param name="name">The <see cref="string" /> to match.</param>
    /// <returns>The <see cref="EnemySetup" /> or null.</returns>
    public static EnemySetup? GetEnemyByName(string? name)
        => EnemyDirector.instance?.GetEnemyByName(name);

    /// <summary>
    ///     Tries to get an <see cref="EnemySetup" /> that contains the name or by its <see cref="EnemyParent" /> that contains
    ///     the name.
    /// </summary>
    /// <param name="name">The <see cref="string" /> to compare.</param>
    /// <param name="enemySetup">The found <see cref="EnemySetup" />.</param>
    /// <returns>Whether the <see cref="EnemySetup" /> was found.</returns>
    public static bool TryGetEnemyThatContainsName(string name, [NotNullWhen(true)] out EnemySetup? enemySetup)
        => (enemySetup = GetEnemyThatContainsName(name)) != null;

    /// <summary>
    ///     Gets an <see cref="EnemySetup" /> that contains the name or by its <see cref="EnemyParent" /> that contains the
    ///     name.
    /// </summary>
    /// <param name="name">The <see cref="string" /> to compare.</param>
    /// <returns>The <see cref="EnemySetup" /> or null.</returns>
    public static EnemySetup? GetEnemyThatContainsName(string name)
        => EnemyDirector.instance?.GetEnemyThatContainsName(name);
}