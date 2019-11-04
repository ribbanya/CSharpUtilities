using System;
using System.Linq;
using Entitas;
using JetBrains.Annotations;

namespace TeamSalvato.Entitas {
  //TODO: Code generation instead of reflection
  [PublicAPI]
  public sealed class EntityBinding<TEntity> where TEntity : class, IEntity {
    public TEntity entity { get; }

    public EntityBinding(TEntity entity) {
      this.entity = entity;
      this.entity.Retain(this);
    }

    public EntityBinding(IContext<TEntity> context) {
      this.entity = context.CreateEntity();
      this.entity.Retain(this);
    }

    public EntityBinding(IContexts contexts) {
      this.entity = GetContextFromContextsByEntityType(contexts).CreateEntity();
    }

    ~EntityBinding() {
      this.entity.Release(this);
    }

    public object this[int index] {
      get => this.entity.GetComponent(index);
      set => this.entity.BindComponent(index, value, true);
    }

    public object this[string componentName] {
      get => this.entity.GetComponent(GetIndexForName(this.entity, componentName));
      set => this.entity.BindComponent(GetIndexForName(this.entity, componentName), value, true);
    }

    private static int GetIndexForName(IEntity entity, string componentName) {
      return Array.IndexOf(entity.contextInfo.componentNames, componentName);
    }

    public static implicit operator TEntity(EntityBinding<TEntity> binding) {
      return binding.entity;
    }

    public static implicit operator EntityBinding<TEntity>(TEntity entity) {
      return new EntityBinding<TEntity>(entity);
    }

    private static IContext<TEntity> GetContextFromContextsByEntityType(IContexts contexts) {
      foreach (var context in contexts.allContexts)
        for (var type = context.GetType(); type != null; type = type.BaseType) {
          if (!type.IsGenericType) continue;
          if (type.GetGenericArguments().Any(argument => argument == typeof(TEntity)))
            return (IContext<TEntity>) context;
        }

      throw new ArgumentException($"Context corresponding to {typeof(TEntity)} in {contexts} was not found.");
    }
  }
}