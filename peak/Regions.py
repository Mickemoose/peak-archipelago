# Regions.py
import logging
from typing import TYPE_CHECKING

from BaseClasses import Region, LocationProgressType, Item, ItemClassification
from .Locations import (
    PeakLocation,
    EXCLUDED_LOCATIONS,
    LOCATION_TABLE
)

if TYPE_CHECKING:
    from . import PeakWorld

def create_peak_regions(world: "PeakWorld"):

    menu_region = Region("Menu", world.player, world.multiworld)
    mountain_region = Region("Mountain", world.player, world.multiworld)

    world.multiworld.regions.extend([menu_region, mountain_region])
    menu_region.connect(mountain_region)

    # Determine which ascent levels should be excluded based on goal settings
    required_ascent = world.options.ascent_count.value
    goal_type = world.options.goal.value
    
    logging.info(f"[Player {world.multiworld.player_name[world.player]}] Goal Type: {goal_type}, Required Ascent: {required_ascent}")
    
    # If goal is "Reach Peak" (0), exclude ascent badges above the required level
    excluded_ascent_levels = set()
    if goal_type == 0:  # Reach Peak goal
        for ascent_level in range(required_ascent + 1, 8):  # Ascents above required level
            excluded_ascent_levels.add(ascent_level)
    
    logging.info(f"[Player {world.multiworld.player_name[world.player]}] Excluded ascent levels: {excluded_ascent_levels}")
    
    excluded_location_count = 0
    created_location_count = 0

    # Add all regular locations from LOCATION_TABLE
    for name, loc_id in LOCATION_TABLE.items():
        # Skip creating locations that should be excluded
        should_skip = False
        if goal_type == 0:  # Only skip if goal is Reach Peak
            for level in excluded_ascent_levels:
                if f"(Ascent {level})" in name:
                    should_skip = True
                    excluded_location_count += 1
                    logging.info(f"[Player {world.multiworld.player_name[world.player]}] SKIPPING CREATION: {name}")
                    break
        
        if should_skip:
            continue  # Don't create this location at all
        
        loc = PeakLocation(world.player, name, loc_id, parent=mountain_region)
        
        # Mark location as excluded if it's in EXCLUDED_LOCATIONS
        if loc_id in EXCLUDED_LOCATIONS:
            loc.progress_type = LocationProgressType.EXCLUDED
        
        mountain_region.locations.append(loc)
        created_location_count += 1

    # Add event locations (no numeric ID) — become progression items when checked
    event_locations = [
        ("Ascent 1 Completed", "Ascent 1 Completed"),
        ("Ascent 2 Completed", "Ascent 2 Completed"),
        ("Ascent 3 Completed", "Ascent 3 Completed"),
        ("Ascent 4 Completed", "Ascent 4 Completed"),
        ("Ascent 5 Completed", "Ascent 5 Completed"),
        ("Ascent 6 Completed", "Ascent 6 Completed"),
        ("Ascent 7 Completed", "Ascent 7 Completed"),
        ("Idol Dunked", "Idol Dunked"),
        ("All Badges Collected", "All Badges Collected"),
        ("Mesa Access", "Mesa Access"),
        ("Roots Access", "Roots Access"),
        ("Alpine Access", "Alpine Access"),
    ]
    
    for loc_name, item_name in event_locations:
        # Skip creating event locations for excluded ascents
        should_skip_event = False
        if goal_type == 0:
            for level in excluded_ascent_levels:
                if f"Ascent {level} Completed" == loc_name:
                    should_skip_event = True
                    excluded_location_count += 1
                    logging.info(f"[Player {world.multiworld.player_name[world.player]}] SKIPPING EVENT CREATION: {loc_name}")
                    break
        
        if should_skip_event:
            continue  # Don't create this event location at all
        
        ev_loc = PeakLocation(world.player, loc_name, None, parent=mountain_region)
        ev_loc.place_locked_item(Item(item_name, ItemClassification.progression, None, world.player))
        mountain_region.locations.append(ev_loc)
        created_location_count += 1

    logging.info(f"[Player {world.multiworld.player_name[world.player]}] Total excluded locations: {excluded_location_count}")
    logging.info(f"[Player {world.multiworld.player_name[world.player]}] Total created locations: {created_location_count}")
    logging.info(f"[Player {world.multiworld.player_name[world.player]}] Created {len(mountain_region.locations)} locations in Mountain region")