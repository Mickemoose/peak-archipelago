import logging
import math
from collections import Counter
import typing

from BaseClasses import ItemClassification, CollectionState
from worlds.AutoWorld import World, WebWorld
from .Items import PeakItem, item_table, progression_table, useful_table, filler_table, trap_table, lookup_id_to_name, item_groups
from .Locations import LOCATION_TABLE, EXCLUDED_LOCATIONS
from .Options import PeakOptions, peak_option_groups
from .Rules import apply_rules

class PeakWeb(WebWorld):
    theme = "stone"
    option_groups = peak_option_groups

class PeakWorld(World):
    """
    PEAK is a multiplayer climbing game where you and your friends must reach the summit of a procedurally generated mountain.
    """
    game = "PEAK"
    options_dataclass = PeakOptions
    options: PeakOptions
    topology_present = False

    item_name_groups = item_groups
    item_name_to_id = {name: data.code for name, data in item_table.items()}
    location_name_to_id = LOCATION_TABLE.copy()
    
    # Add event locations to the mapping
    event_locations = [
        "Ascent 1 Completed",
        "Ascent 2 Completed",
        "Ascent 3 Completed",
        "Ascent 4 Completed",
        "Ascent 5 Completed",
        "Ascent 6 Completed",
        "Ascent 7 Completed",
        "Mesa Access",
        "Alpine Access",
        "Roots Access",
        "Idol Dunked",
        "All Badges Collected"

    ]
    for event_loc in event_locations:
        location_name_to_id[event_loc] = None

    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.excluded_locations = set()

    def validate_ids(self):
        """Ensure that item and location IDs are unique."""
        item_ids = list(self.item_name_to_id.values())
        dupe_items = [item for item, count in Counter(item_ids).items() if count > 1]
        if dupe_items:
            raise Exception(f"Duplicate item IDs found: {dupe_items}")

        loc_ids = [loc_id for loc_id in self.location_name_to_id.values() if loc_id is not None]
        dupe_locs = [loc for loc, count in Counter(loc_ids).items() if count > 1]
        if dupe_locs:
            raise Exception(f"Duplicate location IDs found: {dupe_locs}")

    def create_regions(self):
        """Create regions using the location table."""
        from .Regions import create_peak_regions
        self.validate_ids()
        create_peak_regions(self)

    def create_item(self, name: str, classification: ItemClassification = None) -> PeakItem:
        """Create a Peak item from the given name."""
        if name not in item_table:
            raise ValueError(f"Item '{name}' not found in item_table")
        
        data = item_table[name]
        
        # Use provided classification or default to item's classification
        if classification is None:
            classification = data.classification
            
        return PeakItem(name, classification, data.code, self.player)

    def create_items(self):
        """Create the initial item pool based on the location table."""
        
        total_locations = len(self.location_name_to_id)
        item_pool = []
        
        # Add progression items (Ascent unlocks)
        for item_name in progression_table.keys():
            item_pool.append(self.create_item(item_name))
        
        # Add progressive stamina items if enabled
        if self.options.progressive_stamina.value:
            max_stamina_upgrades = 4
            if self.options.additional_stamina_bars.value:
                max_stamina_upgrades = 7
            
            for i in range(max_stamina_upgrades):
                item_pool.append(self.create_item("Progressive Stamina Bar"))
            
            logging.debug(f"[Player {self.multiworld.player_name[self.player]}] Added {max_stamina_upgrades} progressive stamina items")

        # Add useful items
        for item_name in useful_table.keys():
            if item_name != "Progressive Stamina Bar":  # Skip stamina bar since we handled it above
                item_pool.append(self.create_item(item_name))
        
        # Calculate how many slots are left for traps and fillers
        remaining_slots = total_locations - len(item_pool)
        
        # Build trap_weights list based on individual trap weights
        trap_weights = []
        trap_weights += (["Instant Death Trap"] * self.options.instant_death_trap_weight.value)
        trap_weights += (["Items to Bombs"] * self.options.items_to_bombs_weight.value)
        trap_weights += (["Pokemon Trivia Trap"] * self.options.pokemon_trivia_trap_weight.value)
        trap_weights += (["Blackout Trap"] * self.options.blackout_trap_weight.value)
        trap_weights += (["Spawn Bee Swarm"] * self.options.spawn_bee_swarm_weight.value)
        trap_weights += (["Banana Peel Trap"] * self.options.banana_peel_trap_weight.value)
        trap_weights += (["Minor Poison Trap"] * self.options.minor_poison_trap_weight.value)
        trap_weights += (["Poison Trap"] * self.options.poison_trap_weight.value)
        trap_weights += (["Deadly Poison Trap"] * self.options.deadly_poison_trap_weight.value)
        trap_weights += (["Tornado Trap"] * self.options.tornado_trap_weight.value)
        trap_weights += (["Swap Trap"] * self.options.swap_trap_weight.value)
        trap_weights += (["Nap Time Trap"] * self.options.nap_time_trap_weight.value)
        trap_weights += (["Hungry Hungry Camper Trap"] * self.options.hungry_hungry_camper_trap_weight.value)
        trap_weights += (["Balloon Trap"] * self.options.balloon_trap_weight.value)
        trap_weights += (["Slip Trap"] * self.options.slip_trap_weight.value)
        trap_weights += (["Freeze Trap"] * self.options.freeze_trap_weight.value)
        trap_weights += (["Cold Trap"] * self.options.cold_trap_weight.value)
        trap_weights += (["Hot Trap"] * self.options.hot_trap_weight.value)
        trap_weights += (["Injury Trap"] * self.options.injury_trap_weight.value)
        trap_weights += (["Cactus Ball Trap"] * self.options.cactus_ball_trap_weight.value)
        trap_weights += (["Yeet Trap"] * self.options.yeet_trap_weight.value)
        trap_weights += (["Tumbleweed Trap"] * self.options.tumbleweed_trap_weight.value)
        trap_weights += (["Zombie Horde Trap"] * self.options.zombie_horde_trap_weight.value)
        trap_weights += (["Gust Trap"] * self.options.gust_trap_weight.value)
        trap_weights += (["Mandrake Trap"] * self.options.mandrake_trap_weight.value)
        trap_weights += (["Fungal Infection Trap"] * self.options.fungal_infection_trap_weight.value)
        
        # Calculate number of trap items based on TrapPercentage
        trap_count = 0 if (len(trap_weights) == 0) else math.ceil(remaining_slots * (self.options.trap_percentage.value / 100.0))
        
        # Add trap items by randomly selecting from weighted list
        trap_pool = []
        for i in range(trap_count):
            trap_item = self.multiworld.random.choice(trap_weights)
            trap_pool.append(self.create_item(trap_item))
        
        item_pool += trap_pool
        
        # Fill remaining slots with filler items
        filler_items = list(filler_table.keys())
        while len(item_pool) < total_locations:
            filler_name = self.random.choice(filler_items)
            item_pool.append(self.create_item(filler_name))
        
        logging.debug(f"[Player {self.multiworld.player_name[self.player]}] Total item pool count: {len(item_pool)}")
        logging.debug(f"[Player {self.multiworld.player_name[self.player]}] Total locations: {total_locations}")
        logging.debug(f"[Player {self.multiworld.player_name[self.player]}] Trap items added: {trap_count}")
        
        self.multiworld.itempool.extend(item_pool)
    
    def output_active_traps(self) -> typing.Dict[str, int]:
        trap_data = {}

        trap_data["instant_death_trap"] = self.options.instant_death_trap_weight.value
        trap_data["items_to_bombs"] = self.options.items_to_bombs_weight.value
        trap_data["pokemon_trivia_trap"] = self.options.pokemon_trivia_trap_weight.value
        trap_data["blackout_trap"] = self.options.blackout_trap_weight.value
        trap_data["spawn_bee_swarm"] = self.options.spawn_bee_swarm_weight.value
        trap_data["banana_peel_trap"] = self.options.banana_peel_trap_weight.value
        trap_data["minor_poison_trap"] = self.options.minor_poison_trap_weight.value
        trap_data["poison_trap"] = self.options.poison_trap_weight.value
        trap_data["deadly_poison_trap"] = self.options.deadly_poison_trap_weight.value
        trap_data["tornado_trap"] = self.options.tornado_trap_weight.value
        trap_data["swap_trap"] = self.options.swap_trap_weight.value
        trap_data["nap_time_trap"] = self.options.nap_time_trap_weight.value
        trap_data["hungry_hungry_camper_trap"] = self.options.hungry_hungry_camper_trap_weight.value
        trap_data["balloon_trap"] = self.options.balloon_trap_weight.value
        trap_data["slip_trap"] = self.options.slip_trap_weight.value
        trap_data["freeze_trap"] = self.options.freeze_trap_weight.value
        trap_data["cold_trap"] = self.options.cold_trap_weight.value
        trap_data["hot_trap"] = self.options.hot_trap_weight.value
        trap_data["injury_trap"] = self.options.injury_trap_weight.value
        trap_data["cactus_ball_trap"] = self.options.cactus_ball_trap_weight.value
        trap_data["yeet_trap"] = self.options.yeet_trap_weight.value
        trap_data["tumbleweed_trap"] = self.options.tumbleweed_trap_weight.value
        trap_data["zombie_horde_trap"] = self.options.zombie_horde_trap_weight.value
        trap_data["gust_trap"] = self.options.gust_trap_weight.value
        trap_data["mandrake_trap"] = self.options.mandrake_trap_weight.value
        trap_data["fungal_infection_trap"] = self.options.fungal_infection_trap_weight.value

        return trap_data

    def set_rules(self):
        """Set progression rules and top-up the item pool based on final locations."""

        apply_rules(self)

        # Access options directly via self.options
        goal = self.options.goal.value
        ascent_num = self.options.ascent_count.value

        # Set completion condition based on goal type
        if goal == 0:  # Reach Peak
            if 1 <= ascent_num <= 7:
                self.multiworld.completion_condition[self.player] = (
                    lambda state, n=ascent_num: state.has(f"Ascent {n} Completed", self.player)
                )
            else:
                return  # Invalid ascent count, exit early

        elif goal == 1:  # Complete All Badges
            self.multiworld.completion_condition[self.player] = (
                lambda state: state.has("All Badges Collected", self.player)
            )

        elif goal == 2:  # 24 Karat Badge
            self.multiworld.completion_condition[self.player] = (
                lambda state: state.has("Idol Dunked", self.player)
            )

        else:
            return  # Unsupported goal type, exit early

        # Ensure item pool matches number of locations
        final_locations = [loc for loc in self.multiworld.get_locations() if loc.player == self.player]
        current_items = [item for item in self.multiworld.itempool if item.player == self.player]
        missing = len(final_locations) - len(current_items)

        if missing > 0:
            logging.debug(
                f"[Player {self.multiworld.player_name[self.player]}] "
                f"Item pool is short by {missing} items. Adding filler items."
            )
            for _ in range(missing):
                filler_name = self.get_filler_item_name()
                self.multiworld.itempool.append(self.create_item(filler_name))

    def fill_slot_data(self):
        """Return slot data for this player."""
        slot_data = {
            "goal": self.options.goal.value,
            "ascent_count": self.options.ascent_count.value,
            "badge_count": self.options.badge_count.value,
            "progressive_stamina": self.options.progressive_stamina.value,
            "additional_stamina_bars": self.options.additional_stamina_bars.value,
            "trap_percentage": self.options.trap_percentage.value,
            "ring_link": self.options.ring_link.value,
            "hard_ring_link": self.options.hard_ring_link.value,
            "energy_link": self.options.energy_link.value,
            "trap_link": self.options.trap_link.value,
            "death_link": self.options.death_link.value,
            "death_link_behavior": self.options.death_link_behavior.value,
            "death_link_send_behavior": self.options.death_link_send_behavior.value,
            "active_traps": self.output_active_traps(),
        }
        
        # Log what we're sending
        logging.info(f"[Player {self.multiworld.player_name[self.player]}] Slot data being sent: {slot_data}")
        
        return slot_data

    def get_filler_item_name(self):
        """Randomly select a filler item from the available candidates."""
        if not filler_table:
            raise Exception("No filler items available in item_table.")
        return self.random.choice(list(filler_table.keys()))