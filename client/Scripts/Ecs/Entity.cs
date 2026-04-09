using System;
using System.Collections.Generic;

namespace Game.Ecs;

/// <summary>
/// Lightweight entity: just an integer ID with a component dictionary.
/// </summary>
public class Entity
{
	public int Id { get; }
	public bool IsAlive { get; set; } = true;

	private readonly Dictionary<Type, object> _components = new();

	public Entity(int id)
	{
		Id = id;
	}

	public T Add<T>(T component) where T : class
	{
		_components[typeof(T)] = component;
		return component;
	}

	public T Get<T>() where T : class
	{
		return _components.TryGetValue(typeof(T), out var c) ? (T)c : null;
	}

	public bool Has<T>() where T : class
	{
		return _components.ContainsKey(typeof(T));
	}

	public void Remove<T>() where T : class
	{
		_components.Remove(typeof(T));
	}
}
