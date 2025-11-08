import logging
import math
from collections import Counter

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
        "Lone Wolf Badge", 
        "Peak Badge",
        "Survivalist Badge",
        "Mesa Access",
        "Alpine Access",
        "Roots Access",
        "24 Karat Badge"
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
        
        # Calculate number of trap items based on TrapPercentage
        trap_count = int(remaining_slots * (self.options.trap_percentage.value / 100))
        
        # Add trap items
        if trap_count > 0:
            trap_items = list(trap_table.keys())
            for i in range(trap_count):
                trap_name = trap_items[i % len(trap_items)]
                item_pool.append(self.create_item(trap_name))
        
        # Fill remaining slots with filler items
        filler_items = list(filler_table.keys())
        while len(item_pool) < total_locations:
            filler_name = self.random.choice(filler_items)
            item_pool.append(self.create_item(filler_name))
        
        logging.debug(f"[Player {self.multiworld.player_name[self.player]}] Total item pool count: {len(item_pool)}")
        logging.debug(f"[Player {self.multiworld.player_name[self.player]}] Total locations: {total_locations}")
        logging.debug(f"[Player {self.multiworld.player_name[self.player]}] Trap items added: {trap_count}")
        
        self.multiworld.itempool.extend(item_pool)

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
                lambda state: state.has("Peak Badge", self.player)
            )

        elif goal == 2:  # 24 Karat Badge
            self.multiworld.completion_condition[self.player] = (
                lambda state: state.has("24 Karat Badge", self.player)
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
        }
        
        # Log what we're sending
        logging.info(f"[Player {self.multiworld.player_name[self.player]}] Slot data being sent: {slot_data}")
        
        return slot_data

    def get_filler_item_name(self):
        """Randomly select a filler item from the available candidates."""
        if not filler_table:
            raise Exception("No filler items available in item_table.")
        return self.random.choice(list(filler_table.keys()))