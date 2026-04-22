using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Ecs;

/// <summary>
/// Abstract base class for all ECS systems.
/// </summary>
public abstract class GameSystem
{
	public World World { get; internal set; }
	public virtual void Initialize() { }
	public abstract void Update(float delta);
}

/// <summary>
/// ECS World: manages entities, components, and systems.
/// </summary>
public class World
{
	private int _nextEntityId;
	private readonly Dictionary<int, Entity> _entities = new();
	private readonly List<Entity> _pendingDestroy = new();
	private readonly List<GameSystem> _systems = new();

	public IReadOnlyDictionary<int, Entity> Entities => _entities;

	public Entity CreateEntity()
	{
		var entity = new Entity(_nextEntityId++);
		_entities[entity.Id] = entity;
		return entity;
	}

	public Entity GetEntity(int id)
	{
		return _entities.TryGetValue(id, out var e) ? e : null;
	}

	public void DestroyEntity(int id)
	{
		if (_entities.TryGetValue(id, out var entity))
		{
			entity.IsAlive = false;
			_pendingDestroy.Add(entity);
		}
	}

	/// <summary>
	/// Get all alive entities that have all specified component types.
	/// </summary>
	public List<Entity> GetEntitiesWith<T1>() where T1 : class
	{
		var result = new List<Entity>();
		foreach (var entity in _entities.Values)
		{
			if (entity.IsAlive && entity.Has<T1>())
				result.Add(entity);
		}
		// 排序以保证跨环境枚举顺序一致
		result.Sort((a, b) => a.Id.CompareTo(b.Id));
		return result;
	}

	public List<Entity> GetEntitiesWith<T1, T2>() where T1 : class where T2 : class
	{
		var result = new List<Entity>();
		foreach (var entity in _entities.Values)
		{
			if (entity.IsAlive && entity.Has<T1>() && entity.Has<T2>())
				result.Add(entity);
		}
		result.Sort((a, b) => a.Id.CompareTo(b.Id));
		return result;
	}

	public List<Entity> GetEntitiesWith<T1, T2, T3>() where T1 : class where T2 : class where T3 : class
	{
		var result = new List<Entity>();
		foreach (var entity in _entities.Values)
		{
			if (entity.IsAlive && entity.Has<T1>() && entity.Has<T2>() && entity.Has<T3>())
				result.Add(entity);
		}
		result.Sort((a, b) => a.Id.CompareTo(b.Id));
		return result;
	}

	public void AddSystem(GameSystem system)
	{
		system.World = this;
		system.Initialize();
		_systems.Add(system);
	}

	public T GetSystem<T>() where T : GameSystem
	{
		return _systems.OfType<T>().FirstOrDefault();
	}

	public void Update(float delta)
	{
		foreach (var system in _systems)
		{
			system.Update(delta);
		}

		// Clean up destroyed entities after all systems run
		foreach (var entity in _pendingDestroy)
		{
			_entities.Remove(entity.Id);
		}
		_pendingDestroy.Clear();
	}

	public void Clear()
	{
		_entities.Clear();
		_pendingDestroy.Clear();
		_nextEntityId = 0;
	}
}
