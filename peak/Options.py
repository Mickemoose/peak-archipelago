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
    range_start = 1
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

class BaseTrapWeight(Choice):
    """
    Base Class for Trap Weights
    """
    option_none = 0
    option_low = 1
    option_medium = 2
    option_high = 4
    default = 2

class InstantDeathTrapWeight(BaseTrapWeight):
    """
    Likelihood of receiving a trap which instantly kills a player
    """
    display_name = "InstantDeathTrap Weight"
class ItemsToBombsWeight(BaseTrapWeight):
    """
    Likelihood of receiving a trap which drops the currently held item and replaces it with Dynamite
    """
    display_name = "ItemsToBombs Weight"
class PokemonTriviaTrapWeight(BaseTrapWeight):
    """
    Likelihood of receiving a trap which quizzes the player with Pokemon trivia
    """
    display_name = "PokemonTriviaTrap Weight"
class BlackoutTrapWeight(BaseTrapWeight):
    """
    Likelihood of receiving a trap which blacks out the player's screen temporarily
    """
    display_name = "BlackoutTrap Weight"
class SpawnBeeSwarmWeight(BaseTrapWeight):
    """
    Likelihood of receiving a trap which spawns a swarm of bees
    """
    display_name = "SpawnBeeSwarm Weight"
class BananaPeelTrapWeight(BaseTrapWeight):
    """
    Likelihood of receiving a trap which spawns a berrynana peel in front of a player
    """
    display_name = "BananaPeelTrap Weight"
class MinorPoisonTrapWeight(BaseTrapWeight):
    """
    Likelihood of receiving a trap which applies a weak Poison Affliction to a player
    """
    display_name = "MinorPoisonTrap Weight"
class PoisonTrapWeight(BaseTrapWeight):
    """
    Likelihood of receiving a trap which applies a Poison Affliction to a player
    """
    display_name = "PoisonTrap Weight"
class DeadlyPoisonTrapWeight(BaseTrapWeight):
    """
    Likelihood of receiving a trap which applies a strong Poison Affliction to a player
    """
    display_name = "DeadlyPoisonTrap Weight"   
class TornadoTrapWeight(BaseTrapWeight):
    """
    Likelihood of receiving a trap which spawns a tornado on a player
    """
    display_name = "TornadoTrap Weight"
class SwapTrapWeight(BaseTrapWeight):
    """
    Likelihood of receiving a trap which swaps the position of all players
    """
    display_name = "SwapTrap Weight"
class NapTimeTrapWeight(BaseTrapWeight):
    """
    Likelihood of receiving a trap which applies a Drowsy Affliction to a player
    """
    display_name = "NapTimeTrap Weight"
class HungryHungryCamperTrapWeight(BaseTrapWeight):
    """
    Likelihood of receiving a trap which applies a Hunger Affliction to a player
    """
    display_name = "HungryHungryCamperTrap Weight"
class BalloonTrapWeight(BaseTrapWeight):
    """
    Likelihood of receiving a trap which ties a bundle of balloons to a player
    """
    display_name = "BalloonTrap Weight"
class SlipTrapWeight(BaseTrapWeight):
    """
    Likelihood of receiving a trap which makes a player slip and fall
    """
    display_name = "SlipTrap Weight"
class FreezeTrapWeight(BaseTrapWeight):
    """
    Likelihood of receiving a trap which applies a Cold Affliction to a player
    """
    display_name = "FreezeTrap Weight"
class ColdTrapWeight(BaseTrapWeight):
    """
    Likelihood of receiving a trap which applies a mild Cold Affliction to a player
    """
    display_name = "ColdTrap Weight"
class HotTrapWeight(BaseTrapWeight):
    """
    Likelihood of receiving a trap which applies a Hot Affliction to a player
    """
    display_name = "HotTrap Weight"
class InjuryTrapWeight(BaseTrapWeight):
    """
    Likelihood of receiving a trap which applies an Injury Affliction to a player
    """
    display_name = "InjuryTrap Weight"
class CactusBallTrapWeight(BaseTrapWeight):
    """
    Likelihood of receiving a trap which replaces a players currently held item with a Cactus Ball
    """
    display_name = "CactusBallTrap Weight"
class YeetTrapWeight(BaseTrapWeight):
    """
    Likelihood of receiving a trap which causes a player to throw their currently held item at max force
    """
    display_name = "YeetTrap Weight"
class TumbleweedTrapWeight(BaseTrapWeight):
    """
    Likelihood of receiving a trap which spawns a tumbleweed that chases a player
    """
    display_name = "TumbleweedTrap Weight"
class ZombieHordeTrapWeight(BaseTrapWeight):
    """
    Likelihood of receiving a trap which spawns a horde of mushroom zombies
    """
    display_name = "ZombieHordeTrap Weight"
class GustTrapWeight(BaseTrapWeight):
    """
    Likelihood of receiving a trap which spawns a wind storm that pushes players around
    """
    display_name = "GustTrap Weight"
class MandrakeTrapWeight(BaseTrapWeight):
    """
    Likelihood of receiving a trap which replaces a player's currently held item with a Mandrake
    """
    display_name = "MandrakeTrap Weight"
class FungalInfectionTrapWeight(BaseTrapWeight):
    """
    Likelihood of receiving a trap which applies a Possession Affliction over time to a player
    """
    display_name = "FungalInfectionTrap Weight"
class FearTrapWeight(BaseTrapWeight):
    """
    Likelihood of receiving a spooky trap
    """
    display_name = "FearTrap Weight"

class TrapPercentage(Range):
    """
    Replace a percentage of junk items in the item pool with random traps
    """
    display_name = "Trap Fill Percentage"
    range_start = 0
    range_end = 100
    default = 10


# Option Groups for better organization in the web UI
peak_option_groups = [
    OptionGroup("General Options", [
        Goal,
        AscentCount,
        BadgeCount,
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
    OptionGroup("Traps", [
        TrapPercentage,
        InstantDeathTrapWeight,
        ItemsToBombsWeight,
        PokemonTriviaTrapWeight,
        BlackoutTrapWeight,
        SpawnBeeSwarmWeight,
        BananaPeelTrapWeight,
        MinorPoisonTrapWeight,
        PoisonTrapWeight,
        DeadlyPoisonTrapWeight,
        TornadoTrapWeight,
        SwapTrapWeight,
        NapTimeTrapWeight,
        HungryHungryCamperTrapWeight,
        BalloonTrapWeight,
        SlipTrapWeight,
        FreezeTrapWeight,
        ColdTrapWeight,
        HotTrapWeight,
        InjuryTrapWeight,
        CactusBallTrapWeight,
        YeetTrapWeight,
        TumbleweedTrapWeight,
        ZombieHordeTrapWeight,
        GustTrapWeight,
        MandrakeTrapWeight,
        FungalInfectionTrapWeight,
        FearTrapWeight,
    ]),
]


@dataclass
class PeakOptions(PerGameCommonOptions):
    goal: Goal
    ascent_count: AscentCount
    badge_count: BadgeCount
    progressive_stamina: ProgressiveStamina
    additional_stamina_bars: AdditionalStaminaBars

    ring_link: RingLink
    hard_ring_link: HardRingLink
    energy_link: EnergyLink
    trap_link: TrapLink
    death_link: DeathLink
    death_link_behavior: DeathLinkBehavior
    death_link_send_behavior: DeathLinkSendBehavior

    trap_percentage: TrapPercentage
    instant_death_trap_weight: InstantDeathTrapWeight
    items_to_bombs_weight: ItemsToBombsWeight
    pokemon_trivia_trap_weight: PokemonTriviaTrapWeight
    blackout_trap_weight: BlackoutTrapWeight
    spawn_bee_swarm_weight: SpawnBeeSwarmWeight
    banana_peel_trap_weight: BananaPeelTrapWeight
    minor_poison_trap_weight: MinorPoisonTrapWeight
    poison_trap_weight: PoisonTrapWeight
    deadly_poison_trap_weight: DeadlyPoisonTrapWeight
    tornado_trap_weight: TornadoTrapWeight
    swap_trap_weight: SwapTrapWeight
    nap_time_trap_weight: NapTimeTrapWeight
    hungry_hungry_camper_trap_weight: HungryHungryCamperTrapWeight
    balloon_trap_weight: BalloonTrapWeight
    slip_trap_weight: SlipTrapWeight
    freeze_trap_weight: FreezeTrapWeight
    cold_trap_weight: ColdTrapWeight
    hot_trap_weight: HotTrapWeight
    injury_trap_weight: InjuryTrapWeight
    cactus_ball_trap_weight: CactusBallTrapWeight
    yeet_trap_weight: YeetTrapWeight
    tumbleweed_trap_weight: TumbleweedTrapWeight
    zombie_horde_trap_weight: ZombieHordeTrapWeight
    gust_trap_weight: GustTrapWeight
    mandrake_trap_weight: MandrakeTrapWeight
    fungal_infection_trap_weight: FungalInfectionTrapWeight
    fear_trap_weight: FearTrapWeight