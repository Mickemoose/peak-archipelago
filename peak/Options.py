from dataclasses import dataclass
from Options import Choice, PerGameCommonOptions, Range, Toggle

class Goal(Choice):
    """Set the goal for completion."""
    display_name = "Goal"
    option_reach_peak = 0
    option_complete_all_badges = 1
    option_24_karat_badge = 2
    default = 0

class AscentCount(Range):
    """Select how many ascents are required for completion."""
    display_name = "Required Ascent Count"
    range_start = 0
    range_end = 7
    default = 4

class BadgeCount(Range):
    """Select how many badges are required for completion."""
    display_name = "Required Badge Count"
    range_start = 10
    range_end = 50
    default = 20

class ProgressiveStamina(Toggle):
    """Enable progressive stamina bars. Players start with 25% stamina and unlock 25% more with each upgrade till 100%."""
    display_name = "Progressive Stamina"
    default = 0

class AdditionalStaminaBars(Toggle):
    """Enable 4 additional stamina bars (total of 200% stamina). Only works when Progressive Stamina is enabled."""
    display_name = "Additional Stamina Bars"
    default = 0

class TrapPercentage(Range):
    """
    Set a percentage of how many filler items are replaced with traps here.
    """
    display_name = "Trap Percentage"
    range_start = 0
    range_end = 100
    default = 10

class DeathLink(Toggle):
    """Enable death link mode, affecting all linked players."""
    display_name = "Death Link"
    default = 0

class DeathLinkBehavior(Choice):
    """Choose what happens when DeathLink triggers."""
    display_name = "Death Link Behavior"
    option_reset_run = 0
    option_reset_to_last_checkpoint = 1
    default = 0

@dataclass
class PeakOptions(PerGameCommonOptions):
    goal: Goal
    ascent_count: AscentCount
    badge_count: BadgeCount
    progressive_stamina: ProgressiveStamina
    additional_stamina_bars: AdditionalStaminaBars
    trap_percentage: TrapPercentage
    death_link: DeathLink
    death_link_behavior: DeathLinkBehavior