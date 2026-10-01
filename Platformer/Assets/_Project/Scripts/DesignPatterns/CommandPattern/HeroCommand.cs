using System;
using System.Threading.Tasks;
using UnityEngine;
using static System.Activator;

public abstract class HeroCommand : ICommand
{
    protected readonly IEntity hero;

    protected HeroCommand(IEntity hero)
    {
        this.hero = hero;
    }

    public abstract Task Execute();

    public static T Create<T>(IEntity hero) where T : HeroCommand
    {
        return (T)CreateInstance(typeof(T), hero);
    }
}

public class LightAttackCommand : HeroCommand
{
    readonly string[] animations;
    readonly float comboResetWindow;
    readonly Func<Vector3> getDirection;
    int comboCounter = -1;
    float lastAttackTime;

    public LightAttackCommand(IEntity hero, Func<Vector3> getDirection, string[] animations, float comboResetWindow = 1.5f) : base(hero)
    {
        this.getDirection = getDirection;
        this.animations = animations;
        this.comboResetWindow = comboResetWindow;
    }

    public string Advance()
    {
        if (Time.time - lastAttackTime > comboResetWindow)
            comboCounter = -1;

        lastAttackTime = Time.time;
        comboCounter = (int)Mathf.Repeat(comboCounter + 1, animations.Length);
        return animations[comboCounter];
    }

    public override Task Execute()
    {
        hero.Attack(getDirection());
        return Task.CompletedTask;
    }
}

public class BlastAttackCommand : HeroCommand
{
    readonly string[] animations;
    readonly float comboResetWindow;
    int comboCounter = -1;
    float lastAttackTime;

    public BlastAttackCommand(IEntity hero, string[] animations, float comboResetWindow = 1.5f) : base(hero)
    {
        this.animations = animations;
        this.comboResetWindow = comboResetWindow;
    }

    public string Advance()
    {
        if (Time.time - lastAttackTime > comboResetWindow)
            comboCounter = -1;

        lastAttackTime = Time.time;
        comboCounter = (int)Mathf.Repeat(comboCounter + 1, animations.Length);
        return animations[comboCounter];
    }

    public override Task Execute()
    {
        hero.Attack2();
        return Task.CompletedTask;
    }
}
