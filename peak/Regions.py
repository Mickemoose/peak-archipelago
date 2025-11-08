# Regions.py
import logging
from typing import TYPE_CHECKING

from BaseClasses import Region, LocationProgressType
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

    # Add all regular locations from LOCATION_TABLE
    for name, loc_id in LOCATION_TABLE.items():
        loc = PeakLocation(world.player, name, loc_id, parent=mountain_region)
        # Mark location as excluded if it's in EXCLUDED_LOCATIONS
        if loc_id in EXCLUDED_LOCATIONS:
            loc.progress_type = LocationProgressType.EXCLUDED
        mountain_region.locations.append(loc)

    # Add event locations (no numeric ID) — become progression items when checked
    from BaseClasses import Item, ItemClassification
    
    event_locations = [
        ("Ascent 1 Completed", "Ascent 1 Completed"),
        ("Ascent 2 Completed", "Ascent 2 Completed"),
        ("Ascent 3 Completed", "Ascent 3 Completed"),
        ("Ascent 4 Completed", "Ascent 4 Completed"),
        ("Ascent 5 Completed", "Ascent 5 Completed"),
        ("Ascent 6 Completed", "Ascent 6 Completed"),
        ("Ascent 7 Completed", "Ascent 7 Completed"),
        ("Lone Wolf Badge", "Lone Wolf Badge"),
        ("Peak Badge", "Peak Badge"),
        ("Survivalist Badge", "Survivalist Badge"),
        ("Mesa Access", "Mesa Access"),
        ("Roots Access", "Roots Access"),
        ("Alpine Access", "Alpine Access"),
        ("24 Karat Badge", "24 Karat Badge")
    ]
    
    for loc_name, item_name in event_locations:
        ev_loc = PeakLocation(world.player, loc_name, None, parent=mountain_region)
        ev_loc.place_locked_item(Item(item_name, ItemClassification.progression, None, world.player))
        mountain_region.locations.append(ev_loc)

    logging.debug(f"[Player {world.multiworld.player_name[world.player]}] Created {len(mountain_region.locations)} locations in Mountain region")