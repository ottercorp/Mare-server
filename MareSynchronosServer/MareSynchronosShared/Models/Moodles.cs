using System.ComponentModel.DataAnnotations;

namespace MareSynchronosShared.Models;
[Serializable]
public class Moodles
{
    public enum StatusType
    {
        Positive, Negative, Special
    }
    
    [Flags]
    public enum Modifiers : uint // use uint to allow for futureproof options.
    {
        None                = 0,
        CanDispel           = 1u << 0, // Can be dispelled.
        StacksIncrease      = 1u << 1, // Stackable moodles, when reapplied, can increase their stack count.
        StacksRollOver      = 1u << 2, // When a stack reaches its cap, it starts over and counts up again.
        PersistExpireTime   = 1u << 3, // When reapplied, the expire time remains the same.
        StacksMoveToChain   = 1u << 4, // When a ChainStatus trigger occurs, the current stacks are is carried over.
        StacksCarryToChain  = 1u << 5, // When the stacks increase and hit max, remaining stacks carry over.
        PersistAfterTrigger = 1u << 6, // When a ChainStatus trigger occurs, the original moodle remains.
        // Ideas: Persist original after chain trigger, ext.. 
    }

// What must occur for a chained status to trigger.
// Could be expanded upon to be caused by many things.
    public enum ChainTriggerEnum
    {
        Dispel = 0,
        HitMaxStacks = 1,
        TimerExpired = 2,
    }

    [Key]
    [MaxLength(36)]
    public string GUID { get; set; }
    public int IconID { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public StatusType Type { get; set; }
    
    public string CustomFXPath { get; set; }
    public string Applier { get; set; }
    public bool Dispelable  { get; set; } //
    public int Stacks { get; set; }
    public int StackSteps  { get; set; }
    public Modifiers Modifier { get; set; }
    
    public Guid ChainedStatus { get; set; }
    public ChainTriggerEnum ChainTrigger { get; set; }
    
    public bool StackOnReapply  { get; set; } //
    public int StacksIncOnReapply  { get; set; } //
    
    public int Days { get; set; }
    public int Hours { get; set; }
    public int Minutes { get; set; }
    public int Seconds { get; set; }
    public bool NoExpire { get; set; }
    public bool AsPermanent { get; set; }

    public User User { get; set; }
    public string UserUID { get; set; }

}