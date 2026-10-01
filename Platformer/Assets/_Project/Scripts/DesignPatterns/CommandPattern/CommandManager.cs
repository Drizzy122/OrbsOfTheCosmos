using System.Collections.Generic;
using System.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

public class CommandManager : SerializedMonoBehaviour
{
    public IEntity Entity;
    public ICommand singleCommand;
    public List<ICommand> commands = new List<ICommand>();

    [Header("Light Attack Settings")]
    [SerializeField] string[] lightAttackAnimations = { "Attack1", "Attack2", "Attack3" };

    [Header("Blast Attack Settings")]
    [SerializeField] string[] blastAttackAnimations = { "BlastAttack1", "BlastAttack2", "BlastAttack3" };

    [SerializeField] float comboResetWindow = 1.5f;

    readonly CommandInvoker commandInvoker = new();
    bool isCommandExecuting;

    public LightAttackCommand LightAttackCommand { get; private set; }
    public BlastAttackCommand BlastAttackCommand { get; private set; }

    void Start()
    {
        Entity = GetComponent<IEntity>();
        var controller = GetComponent<Platformer.PlayerMovement>();

        var combat = GetComponent<Platformer.PlayerCombat>();
        LightAttackCommand = new LightAttackCommand(Entity, () => controller.GetAdjustedMovementDirection(), lightAttackAnimations, comboResetWindow);
        BlastAttackCommand = new BlastAttackCommand(Entity, blastAttackAnimations, comboResetWindow);

        singleCommand = LightAttackCommand;
        commands = new List<ICommand> { LightAttackCommand, BlastAttackCommand };
    }

    public async void Execute(ICommand command)
    {
        if (isCommandExecuting) return;
        isCommandExecuting = true;
        await commandInvoker.ExecuteCommand(new List<ICommand> { command });
        isCommandExecuting = false;
    }
}
