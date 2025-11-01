import logging
import math
from collections import Counter

from BaseClasses import ItemClassification, CollectionState
from worlds.AutoWorld import World
from .Items import ITEMS, PeakItem
from .Locations import LOCATION_TABLE, EXCLUDED_LOCATIONS
from .Options import PeakOptions
from .Rules import (
    apply_rules
)


class PeakWorld(World):
    game = "PEAK"
    options_dataclass = PeakOptions
    options: PeakOptions

    # Pre-calculate mappings for items and locations.
    item_name_to_id = {name: data[0] for name, data in ITEMS.items()}
    location_name_to_id = LOCATION_TABLE.copy()
    
    # Add event locations to the mapping (they have None as ID)
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
        "Alpine Access"
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

    def create_item(self, name: str, classification: ItemClassification = ItemClassification.filler) -> PeakItem:
        """Create a Peak item from the given name."""
        if name in self.item_name_to_id:
            item_id = self.item_name_to_id[name]
        else:
            raise ValueError(f"Item '{name}' not found in ITEMS")
        return PeakItem(name, classification, item_id, self.player)

    def create_items(self):
        """Create the initial item pool based on the location table."""
        
        total_locations = len(self.location_name_to_id)
        item_pool = []
        
        # Add progression items (Ascent unlocks)
        for i in range(1, 8):  # Ascent 1-7 unlocks
            item_pool.append(self.create_item(f"Ascent {i} Unlock", classification=ItemClassification.progression))
        
        # Add useful items
        useful_items = [name for name, (code, classification) in ITEMS.items() if classification == ItemClassification.useful]
        for item_name in useful_items:
            item_pool.append(self.create_item(item_name, classification=ItemClassification.useful))
        
        # Calculate how many slots are left for traps and fillers
        remaining_slots = total_locations - len(item_pool)
        
        # Calculate number of trap items based on TrapPercentage
        trap_count = int(remaining_slots * (self.options.trap_percentage.value / 100))
        
        # Add trap items
        if trap_count > 0:
            trap_items = [name for name, (code, classification) in ITEMS.items() if classification == ItemClassification.trap]
            trap_index = 0
            for _ in range(trap_count):
                trap_name = trap_items[trap_index % len(trap_items)]
                item_pool.append(self.create_item(trap_name, classification=ItemClassification.trap))
                trap_index += 1
        
        # Fill remaining slots with filler items
        while len(item_pool) < total_locations:
            filler_name = self.get_filler_item_name()
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
            # DUNK THE IDOL IN THE HOT DRINK
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
        options_dict = self.options.as_dict()
        return options_dict

    def get_filler_item_name(self):
        """Randomly select a filler item from the available candidates."""
        filler_candidates = [
            name for name, (code, classification) in ITEMS.items()
            if classification == ItemClassification.filler
        ]
        if not filler_candidates:
            raise Exception("No filler items available in ITEMS.")
        return self.random.choice(filler_candidates)