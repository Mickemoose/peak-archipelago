import logging
import math
from collections import Counter
import typing

from BaseClasses import ItemClassification, CollectionState, LocationProgressType, Tutorial
from worlds.AutoWorld import World, WebWorld
from .Items import PeakItem, item_table, progression_table, useful_table, filler_table, trap_table, unlock_table, lookup_id_to_name, item_groups
from .Locations import LOCATION_TABLE, EXCLUDED_LOCATIONS
from .Options import PeakOptions, peak_option_groups
from .Rules import apply_rules, TROPICS_LOCATIONS, MESA_LOCATIONS, ALPINE_LOCATIONS, ROOTS_LOCATIONS, CALDERA_LOCATIONS, KILN_LOCATIONS

TRAP_DEFINITIONS = [
    ("Instant Death Trap", "instant_death_trap_weight"),
    ("Items to Bombs", "items_to_bombs_weight"),
    ("Pokemon Trivia Trap", "pokemon_trivia_trap_weight"),
    ("Blackout Trap", "blackout_trap_weight"),
    ("Spawn Bee Swarm", "spawn_bee_swarm_weight"),
    ("Banana Peel Trap", "banana_peel_trap_weight"),
    ("Minor Poison Trap", "minor_poison_trap_weight"),
    ("Poison Trap", "poison_trap_weight"),
    ("Deadly Poison Trap", "deadly_poison_trap_weight"),
    ("Tornado Trap", "tornado_trap_weight"),
    ("Swap Trap", "swap_trap_weight"),
    ("Nap Time Trap", "nap_time_trap_weight"),
    ("Hungry Hungry Camper Trap", "hungry_hungry_camper_trap_weight"),
    ("Balloon Trap", "balloon_trap_weight"),
    ("Slip Trap", "slip_trap_weight"),
    ("Freeze Trap", "freeze_trap_weight"),
    ("Cold Trap", "cold_trap_weight"),
    ("Hot Trap", "hot_trap_weight"),
    ("Injury Trap", "injury_trap_weight"),
    ("Cactus Ball Trap", "cactus_ball_trap_weight"),
    ("Yeet Trap", "yeet_trap_weight"),
    ("Tumbleweed Trap", "tumbleweed_trap_weight"),
    ("Zombie Horde Trap", "zombie_horde_trap_weight"),
    ("Gust Trap", "gust_trap_weight"),
    ("Mandrake Trap", "mandrake_trap_weight"),
    ("Fungal Infection Trap", "fungal_infection_trap_weight"),
    ("Fear Trap", "fear_trap_weight"),
    ("Scoutmaster Trap", "scoutmaster_trap_weight"),
    ("Zoom Trap", "zoom_trap_weight"),
    ("Screen Flip Trap", "screen_flip_trap_weight"),
    ("Drop Everything Trap", "drop_everything_trap_weight"),
    ("Pixel Trap", "pixel_trap_weight"),
    ("Eruption Trap", "eruption_trap_weight"),
    ("Beetle Horde Trap", "beetle_horde_trap_weight"),
    ("Custom Trivia Trap", "custom_trivia_trap_weight"),
    ("Pokemon Count Trap", "pokemon_count_trap_weight"),
    ("Inverted Mouse Trap", "inverted_mouse_trap_weight"),
    ("Stamina Drain Trap", "stamina_drain_trap_weight"),
    ("Chaos Control Trap", "chaos_control_trap_weight"),
    ("Emergency Rescue Trap", "emergency_rescue_trap_weight"),
    ("Explosion Trap", "explosion_trap_weight"),
]

class PeakWeb(WebWorld):
    theme = "stone"
    option_groups = peak_option_groups

    setup_en = Tutorial(
        "Multiworld Setup Guide",
        "A guide to setting up the Archipelago randomizer for PEAK.",
        "English",
        "setup_en.md",
        "setup/en",
        ["Mickemoose"]
    )

    tutorials = [setup_en]

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

    web = PeakWeb()

    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)

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
        
        goal_type = self.options.goal.value
        required_ascent = self.options.ascent_count.value

        total_locations = sum(1 for loc in self.multiworld.get_locations(self.player) if loc.address is not None)
        logging.debug(f"[Player {self.multiworld.player_name[self.player]}] Total locations from created regions: {total_locations}")
        
        item_pool = []
        
        # Add Progressive Ascent items based on goal requirements
        if goal_type == 0 or goal_type == 3:  # Reach Peak goal - only add enough Progressive Ascent for the required level
            for _ in range(required_ascent):
                item_pool.append(self.create_item("Progressive Ascent"))
            logging.debug(f"[Player {self.multiworld.player_name[self.player]}] Added {required_ascent} Progressive Ascent items (Reach Peak goal)")
        else:  # Other goals - add all 7 Progressive Ascent items
            for _ in range(7):
                item_pool.append(self.create_item("Progressive Ascent"))
            logging.debug(f"[Player {self.multiworld.player_name[self.player]}] Added 7 Progressive Ascent items (non-Reach Peak goal)")

        for _ in range(4):
            item_pool.append(self.create_item("Progressive Mountain"))
        logging.debug(f"[Player {self.multiworld.player_name[self.player]}] Added 4 Progressive Mountain items")
    
        for _ in range(8):
            item_pool.append(self.create_item("Progressive Endurance"))
        logging.debug(f"[Player {self.multiworld.player_name[self.player]}] Added 8 Progressive Endurance items")
    

        # Add progressive stamina items if enabled
        if self.options.progressive_stamina.value:
            max_stamina_upgrades = 3
            if self.options.additional_stamina_bars.value:
                max_stamina_upgrades = 7
            
            for i in range(max_stamina_upgrades):
                item_pool.append(self.create_item("Progressive Stamina Bar"))
            
            logging.debug(f"[Player {self.multiworld.player_name[self.player]}] Added {max_stamina_upgrades} progressive stamina items")

        # Add useful items
        for item_name in useful_table.keys():
            if item_name != "Progressive Stamina Bar":  # Skip stamina bar since we handled it above
                item_pool.append(self.create_item(item_name))
        # Add unlock items only when ItemSanity is enabled
        if self.options.item_sanity.value:
            for unlock_name in unlock_table.keys():
                item_pool.append(self.create_item(unlock_name))
            logging.debug(f"[Player {self.multiworld.player_name[self.player]}] Added {len(unlock_table)} unlock items (ItemSanity enabled)")
        else:
            logging.debug(f"[Player {self.multiworld.player_name[self.player]}] Skipping unlock items (ItemSanity disabled)")
        # Calculate how many slots are left for traps and fillers
        remaining_slots = total_locations - len(item_pool)
        
        # Build trap_weights list based on individual trap weights
        trap_weights = []
        for trap_name, weight_attr in TRAP_DEFINITIONS:
            trap_weights += [trap_name] * getattr(self.options, weight_attr).value

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
        
        if len(item_pool) > total_locations:
            raise Exception(
                f"[PEAK] Item pool ({len(item_pool)}) exceeds fillable locations ({total_locations}) for "
                f"player {self.multiworld.player_name[self.player]}. Too many locations are excluded "
                f"(exclude_locations / disable_*_badges / low ascent goal); reduce exclusions or disable item_sanity."
            )

        self.multiworld.itempool.extend(item_pool)
    
    def output_active_traps(self) -> typing.Dict[str, int]:
        trap_data = {}

        for trap_name, weight_attr in TRAP_DEFINITIONS:
            trap_data[weight_attr[:-len("_weight")]] = getattr(self.options, weight_attr).value

        return trap_data

    def set_rules(self):
        """Set progression rules and top-up the item pool based on final locations."""

        apply_rules(self)

        player = self.player
        # Count total Progressive items we're placing
        prog_ascent_count = 7 if self.options.goal.value != 0 and self.options.goal.value != 3 else self.options.ascent_count.value
        prog_stamina_count = 0
        if self.options.progressive_stamina.value:
            prog_stamina_count = 7 if self.options.additional_stamina_bars.value else 4
        prog_endurance_count = 8
        
        shore_accessible_locations = []
        for location in self.multiworld.get_locations(player):
            if location.progress_type == LocationProgressType.EXCLUDED:
                continue
            
            # Skip event locations
            if location.address is None:
                continue
                
            # If it's not in a biome list, it's shore-accessible
            if (location.name not in TROPICS_LOCATIONS and 
                location.name not in ROOTS_LOCATIONS and
                location.name not in MESA_LOCATIONS and
                location.name not in ALPINE_LOCATIONS and
                location.name not in CALDERA_LOCATIONS and
                location.name not in KILN_LOCATIONS and
                "(Ascent" not in location.name):
                shore_accessible_locations.append(location)
        
        logging.info(f"[Player {self.multiworld.player_name[player]}] Found {len(shore_accessible_locations)} shore-accessible locations")
        
        def make_biome_mountain_rule(threshold, loc):
            def biome_rule(item, loc=loc):
                if item.player != player:
                    return True
                if item.name == "Progressive Mountain":
                    if "napberry" not in loc.name.lower():
                        if ("berry" in loc.name.lower() or
                            "conch" in loc.name.lower() or
                            "binoculars" in loc.name.lower() or
                            "guidebook" in loc.name.lower()):
                            return False
                    mountains_in_pool = sum(1 for i in self.multiworld.itempool if i.player == player and i.name == "Progressive Mountain")
                    return mountains_in_pool >= threshold
                return True
            return biome_rule

        # Set item placement rules
        for location in self.multiworld.get_locations(player):
            if location.progress_type == LocationProgressType.EXCLUDED:
                continue
                
            if "(Ascent" in location.name or "Scout sashe" in location.name:
                import re
                match = re.search(r'Ascent (\d+)', location.name)
                if match:
                    required_ascents = int(match.group(1))
                    def make_rule(req_asc, req_stam=0, req_end=0):
                        def rule(item):

                            # prevent these items entirely on high ascents
                            if item.player != player:
                                return True
                            
                            # NEVER place Progressive Mountain on ANY ascent location
                            if item.name == "Progressive Mountain":
                                return False
                            
                            # For high ascents, be conservative
                            if req_asc >= 5:
                                # Don't place any progression items here
                                if item.name in ["Progressive Ascent", "Progressive Stamina Bar", "Progressive Endurance"]:
                                    return False
                            elif req_asc >= 3:
                                # Don't place stamina or ascent here
                                if item.name in ["Progressive Ascent", "Progressive Stamina Bar"]:
                                    return False
                            elif req_asc >= 1:
                                # Don't place ascent here
                                if item.name == "Progressive Ascent":
                                    return False
                            
                            return True
                        return rule
                    
                    # Apply rules based on ascent requirements
                    if required_ascents >= 6:
                        location.item_rule = make_rule(required_ascents, 3, 4)
                    elif required_ascents >= 3:
                        location.item_rule = make_rule(required_ascents, 3, 0)
                    else:
                        location.item_rule = make_rule(required_ascents, 0, 0)

            if location.name in TROPICS_LOCATIONS or location.name in ROOTS_LOCATIONS:
                location.item_rule = make_biome_mountain_rule(2, location)

            elif location.name in ALPINE_LOCATIONS or location.name in MESA_LOCATIONS:
                location.item_rule = make_biome_mountain_rule(3, location)

            elif location.name in CALDERA_LOCATIONS:
                def biome_rule_3(item):
                    if item.player != player:
                        return True
                    if item.classification & ItemClassification.progression:
                        return False
                    return True
                location.item_rule = biome_rule_3

            elif location.name in KILN_LOCATIONS:
                def biome_rule_kiln(item):
                    if item.player != player:
                        return True
                    if item.classification & ItemClassification.progression:
                        return False
                    return True
                location.item_rule = biome_rule_kiln
            
            # Limit Progressive Mountains in shore-accessible locations
            if location in shore_accessible_locations:
                old_rule = location.item_rule
                
                def shore_mountain_limit(item):
                    if item.player != player:
                        return True
                    
                    if item.name == "Progressive Mountain":
                        # Count how many mountains are already placed in shore locations
                        placed_count = sum(1 for loc in shore_accessible_locations 
                                        if loc.item and loc.item.name == "Progressive Mountain" and loc.item.player == player)
                        
                        # Only allow if we haven't hit the limit (max 2 in shore)
                        return placed_count < 2
                    
                    return True
                
                # Combine with existing rule if present
                if old_rule:
                    location.item_rule = lambda item, old=old_rule, shore_limit=shore_mountain_limit: old(item) and shore_limit(item)
                else:
                    location.item_rule = shore_mountain_limit

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
                return 

        elif goal == 1:  # Complete All Badges
            self.multiworld.completion_condition[self.player] = (
                lambda state: state.has("All Badges Collected", self.player)
            )

        elif goal == 2:  # 24 Karat Badge
            self.multiworld.completion_condition[self.player] = (
                lambda state: state.has("Idol Dunked", self.player)
            )
        elif goal == 3:  # Peak and Badges
            if 1 <= ascent_num <= 7:
                self.multiworld.completion_condition[self.player] = (
                    lambda state, n=ascent_num: state.has(f"Ascent {n} Completed", self.player) and state.has("All Badges Collected", self.player)
                )
        else:
            return  # Unsupported goal type, exit early

        return

    def fill_slot_data(self):
        """Return slot data for this player."""
        session_id = f"{self.multiworld.seed_name}_{self.player}"
        
        # Calculate actual badge count from locations that exist in this seed
        badge_locations = [loc for loc in self.multiworld.get_locations(self.player) 
                        if loc.name.endswith(" Badge") and loc.address is not None]
        max_badges_available = len(badge_locations)
        
        # Respect the option but clamp to what's actually available
        requested_badge_count = self.options.badge_count.value
        actual_badge_count = min(requested_badge_count, max_badges_available)

        mountain_hints = []
        for location in self.multiworld.get_locations():
            if location.item and location.item.name == "Progressive Mountain" and location.item.player == self.player:
                mountain_hints.append({
                    "location": location.name,
                    "player": self.multiworld.get_player_name(location.player),
                    "game": self.multiworld.game[location.player],
                    "location_id": location.address,
                    "player_slot": location.player
                })
        
        slot_data = {
            "goal": self.options.goal.value,
            "ascent_count": self.options.ascent_count.value,
            "badge_count": actual_badge_count,
            "progressive_stamina": self.options.progressive_stamina.value,
            "additional_stamina_bars": self.options.additional_stamina_bars.value,
            "trap_percentage": self.options.trap_percentage.value,
            "ring_link": self.options.ring_link.value,
            "hard_ring_link": self.options.hard_ring_link.value,
            "energy_link": self.options.energy_link.value,
            "trap_link": self.options.trap_link.value,
            "breath_link": self.options.breath_link.value,
            "death_link": self.options.death_link.value,
            "death_link_behavior": self.options.death_link_behavior.value,
            "death_link_send_behavior": self.options.death_link_send_behavior.value,
            "active_traps": self.output_active_traps(),
            "item_sanity": self.options.item_sanity.value,
            "loot_sanity": self.options.loot_sanity.value,
            "logical_scout_statue": self.options.logical_scout_statue.value,
            "session_id": session_id,
            "mountain_hints": mountain_hints,
            "loot_biome_assignments": getattr(self, "loot_biome_assignments", {})
        }
        
        # Log what we're sending
        logging.info(f"[Player {self.multiworld.player_name[self.player]}] Slot data being sent: {slot_data}")
        if requested_badge_count > max_badges_available:
            logging.warning(f"[Player {self.multiworld.player_name[self.player]}] Requested {requested_badge_count} badges but only {max_badges_available} available in seed. Clamped to {actual_badge_count}")
        
        return slot_data

    def get_filler_item_name(self):
        """Randomly select a filler item from the available candidates."""
        if not filler_table:
            raise Exception("No filler items available in item_table.")
        return self.random.choice(list(filler_table.keys()))