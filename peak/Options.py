from dataclasses import dataclass
from Options import Choice, PerGameCommonOptions, Range, Toggle, DeathLink, OptionGroup


class Goal(Choice):
    """
    Determines the goal of the seed

    Reach Peak: Reach the peak on the specified ascent level

    Complete All Badges: Collect the specified number of badges

    24 Karat Badge: Toss the Ancient Idol into The Kiln's lava.
    """
    display_name = "Goal"
    option_reach_peak = 0
    option_complete_all_badges = 1
    option_24_karat_badge = 2
    default = 0


class AscentCount(Range):
    """
    The ascent level required to complete the Reach Peak goal (0-7)
    
    Higher ascents add more difficulty modifiers and challenges
    """
    display_name = "Required Ascent Count"
    range_start = 0
    range_end = 7
    default = 4


class BadgeCount(Range):
    """
    The number of badges required to complete the Complete All Badges goal
    
    There are 54 total badges available in the game
    """
    display_name = "Required Badge Count"
    range_start = 10
    range_end = 54
    default = 20


class ProgressiveStamina(Toggle):
    """
    Enable progressive stamina bars
    
    Players start with 25% stamina and unlock 25% more with each upgrade until reaching 100%
    """
    display_name = "Progressive Stamina"


class AdditionalStaminaBars(Toggle):
    """
    Enable 4 additional stamina bars beyond the base 100%
    
    Allows players to reach up to 200% total stamina (requires Progressive Stamina to be enabled)
    """
    display_name = "Additional Stamina Bars"


class TrapPercentage(Range):
    """
    Replace a percentage of junk items in the item pool with random traps
    """
    display_name = "Trap Percentage"
    range_start = 0
    range_end = 100
    default = 10


class RingLink(Toggle):
    """
    When enabled, ring pickups are shared among all players with Ring Link enabled
    
    For PEAK; rings are stamina. Consuming food will send Rings to other players with Ring Link enabled. Poisonous food will send negative rings.
    """
    display_name = "Ring Link"


class HardRingLink(Toggle):
    """
    When enabled, ring pickups are shared among all players with Hard Ring Link enabled
    
    Similar to Ring Link, but instead of sending rings when consuming food, rings are sent from certain actions and events.
    """
    display_name = "Hard Ring Link"


class EnergyLink(Toggle):
    """
    When enabled, allows sending and receiving energy from a shared server pool
    
    Players can contribute stamina to help others in need
    """
    display_name = "Energy Link"


class TrapLink(Toggle):
    """
    When enabled, traps you receive are also sent to other players with Trap Link enabled
    
    You will also receive any linked traps from other players with Trap Link enabled,
    if you have a weight above "none" set for that trap
    """
    display_name = "Trap Link"


class DeathLinkBehavior(Choice):
    """
    Determines what happens when a Death Link is received
    
    Kill Random Player: A random player in your lobby will be killed
    
    Reset to Last Checkpoint: All players will be teleported to the last checkpoint/campfire
    """
    display_name = "Death Link Behavior"
    option_kill_random_player = 0
    option_reset_to_last_checkpoint = 1
    default = 0


class DeathLinkSendBehavior(Choice):
    """
    Determines when Death Links are sent to other players
    
    Any Player Dies: Send Death Link whenever any player in your game dies
    
    All Players Dead: Send Death Link only when all players are dead (game over)
    """
    display_name = "Death Link Send Behavior"
    option_any_player_dies = 0
    option_all_players_dead = 1
    default = 0



# Option Groups for better organization in the web UI
peak_option_groups = [
    OptionGroup("General Options", [
        Goal,
        AscentCount,
        BadgeCount,
        TrapPercentage,
    ]),
    OptionGroup("Stamina", [
        ProgressiveStamina,
        AdditionalStaminaBars,
    ]),
    OptionGroup("Multiplayer Links", [
        RingLink,
        HardRingLink,
        EnergyLink,
        TrapLink,
        DeathLink,
        DeathLinkBehavior,
        DeathLinkSendBehavior,
    ]),
]


@dataclass
class PeakOptions(PerGameCommonOptions):
    goal: Goal
    ascent_count: AscentCount
    badge_count: BadgeCount
    progressive_stamina: ProgressiveStamina
    additional_stamina_bars: AdditionalStaminaBars
    trap_percentage: TrapPercentage
    ring_link: RingLink
    hard_ring_link: HardRingLink
    energy_link: EnergyLink
    trap_link: TrapLink
    death_link: DeathLink
    death_link_behavior: DeathLinkBehavior
    death_link_send_behavior: DeathLinkSendBehavior