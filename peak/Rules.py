from typing import TYPE_CHECKING

from BaseClasses import Location, Entrance

if TYPE_CHECKING:
    from . import PeakWorld

def set_rule(spot: Location | Entrance, rule):
    spot.access_rule = rule


def add_rule(spot: Location | Entrance, rule, combine="and"):
    old_rule = spot.access_rule
    if old_rule is Location.access_rule:
        spot.access_rule = rule if combine == "and" else old_rule
    else:
        if combine == "and":
            spot.access_rule = lambda state: rule(state) and old_rule(state)
        else:
            spot.access_rule = lambda state: rule(state) or old_rule(state)


def apply_rules(world: "PeakWorld"):
    """Apply all access rules for Peak locations."""
    player = world.player
    
    # All regular badge locations are always accessible
    regular_badges = [
        "Astronomy Badge", "24 Karat Badge", "Gourmand Badge", "Daredevil Badge",
        "Mycology Badge", "Megaentomology Badge", "Speed Climber Badge", "Bookwork Badge",
        "Balloon Badge", "Nomad Badge", "Animal Serenading Badge", "Arborist Badge",
        "Endurance Badge", "Toxicology Badge", "Foraging Badge", "Bouldering Badge",
        "Bing Bong Badge", "Cooking Badge", "Plunderer Badge", "High Altitude Badge",
        "Volcanology Badge", "Alpinist Badge", "Esoterica Badge", "Trailblazer Badge",
        "Beachcomber Badge", "Mentorship Badge", "Cool Cucumber Badge", "Naturalist Badge",
        "Aeronautics Badge", "Leave No Trace Badge", "Needlepoint Badge", "Knot Tying Badge",
        "Bundled Up Badge"
    ]
    
    for badge_name in regular_badges:
        try:
            set_rule(world.get_location(badge_name), lambda state: True)
        except KeyError:
            pass
    
    # Luggage locations are always accessible
    luggage_locations = [
        "Open 1 luggage", "Open 5 luggage in a single run", "Open 10 luggage",
        "Open 10 luggage in a single run", "Open 20 luggage in a single run",
        "Open 25 luggage", "Open 50 luggage"
    ]
    
    for luggage_name in luggage_locations:
        try:
            set_rule(world.get_location(luggage_name), lambda state: True)
        except KeyError:
            pass
    
    # Ascent locations require their corresponding Ascent Completed events
    roman_numerals = ["II", "III", "IV", "V", "VI", "VII", "VIII"]
    
    for ascent_num in range(1, 8):  # Ascents 1-7
        roman_num = roman_numerals[ascent_num - 1]  # Convert to 0-based index
        ascent_locations = [
            f"Beachcomber {roman_num} Badge (Ascent {ascent_num})", 
            f"Trailblazer {roman_num} Badge (Ascent {ascent_num})",
            f"Desolate {roman_num} Badge (Ascent {ascent_num})", 
            f"Volcanology {roman_num} Badge (Ascent {ascent_num})"
        ]
        
        for ascent_name in ascent_locations:
            try:
                set_rule(world.get_location(ascent_name), 
                        lambda state, asc=ascent_num: state.has(f"Ascent {asc} Completed", player))
            except KeyError:
                pass
    
    # Event locations require their corresponding unlock items
    for ascent_num in range(1, 8):  # Ascents 1-7
        try:
            set_rule(world.get_location(f"Ascent {ascent_num} Completed"), 
                    lambda state, asc=ascent_num: state.has(f"Ascent {asc} Unlock", player))
        except KeyError:
            pass
    
    # Acquire locations - most are always accessible
    acquire_locations = [
        "Acquire Rope Spool", "Acquire Rope Cannon", "Acquire Anti-Rope Spool", "Acquire Anti-Rope Cannon",
        "Acquire Chain Launcher", "Acquire Piton", "Acquire Magic Bean", "Acquire Parasol",
        "Acquire Balloon", "Acquire Balloon Bunch", "Acquire Scout Cannon", "Acquire Portable Stove",
        "Acquire Campfire", "Acquire Lantern", "Acquire Flare", "Acquire Torch",
        "Acquire Compass", "Acquire Pirate's Compass", "Acquire Binoculars", "Acquire Flying Disc",
        "Acquire Bandages", "Acquire First-Aid Kit", "Acquire Antidote", "Acquire Heat Pack",
        "Acquire Cure-All", "Acquire Faerie Lantern", "Acquire Scout Effigy",
        "Acquire Cursed Skull", "Acquire Pandora's Lunchbox", "Acquire Bugle of Friendship",
        "Acquire Bugle", "Acquire Remedy Fungus", "Acquire Medicinal Root", "Acquire Guidebook",
        "Acquire Shelf Shroom", "Acquire Bounce Shroom", "Acquire Trail Mix",
        "Acquire Granola Bar", "Acquire Scout Cookies", "Acquire Airline Food", "Acquire Energy Drink",
        "Acquire Sports Drink", "Acquire Big Lollipop", "Acquire Button Shroom", "Acquire Bugle Shroom",
        "Acquire Cluster Shroom", "Acquire Chubby Shroom", "Acquire Conch", "Acquire Banana Peel",
        "Acquire Dynamite", "Acquire Bing Bong", "Acquire Red Crispberry", "Acquire Green Crispberry",
        "Acquire Yellow Crispberry", "Acquire Coconut", "Acquire Coconut Half", "Acquire Brown Berrynana",
        "Acquire Blue Berrynana", "Acquire Pink Berrynana", "Acquire Yellow Berrynana", "Acquire Yellow Winterberry",
        "Acquire Strange Gem", "Acquire Egg", "Acquire Turkey"
    ]
    
    for acquire_name in acquire_locations:
        try:
            set_rule(world.get_location(acquire_name), lambda state: True)
        except KeyError:
            pass
    
    # Peak Badge is always accessible
    try:
        set_rule(world.get_location("Peak Badge"), lambda state: True)
    except KeyError:
        pass
    
    # Mesa-locked items require Mesa Access
    mesa_locked_items = [
        "Acquire Cactus", "Acquire Aloe Vera", "Acquire Sunscreen", "Acquire Ancient Idol"
    ]
    
    for mesa_item in mesa_locked_items:
        try:
            set_rule(world.get_location(mesa_item), 
                    lambda state: state.has("Mesa Access", player))
        except KeyError:
            pass
    
    # Alpine-locked items require Alpine Access
    alpine_locked_items = [
        "Acquire Orange Winterberry"
    ]
    
    for alpine_item in alpine_locked_items:
        try:
            set_rule(world.get_location(alpine_item), 
                    lambda state: state.has("Alpine Access", player))
        except KeyError:
            pass
    
    # Scout sashe locations require all previous ascents to be completed
    scout_sashe_requirements = {
        "Rabbit Scout sashe (Ascent 1)": [],  # No requirements
        "Raccoon Scout sashe (Ascent 2)": ["Ascent 1 Completed"],
        "Mule Scout sashe (Ascent 3)": ["Ascent 1 Completed", "Ascent 2 Completed"],
        "Kangaroo Scout sashe (Ascent 4)": ["Ascent 1 Completed", "Ascent 2 Completed", "Ascent 3 Completed"],
        "Owl Scout sashe (Ascent 5)": ["Ascent 1 Completed", "Ascent 2 Completed", "Ascent 3 Completed", "Ascent 4 Completed"],
        "Wolf Scout sashe (Ascent 6)": ["Ascent 1 Completed", "Ascent 2 Completed", "Ascent 3 Completed", "Ascent 4 Completed", "Ascent 5 Completed"],
        "Goat Scout sashe (Ascent 7)": ["Ascent 1 Completed", "Ascent 2 Completed", "Ascent 3 Completed", "Ascent 4 Completed", "Ascent 5 Completed", "Ascent 6 Completed"]
    }
    
    for scout_name, required_ascents in scout_sashe_requirements.items():
        try:
            if not required_ascents:  # Rabbit Scout sashe (Ascent 1) has no requirements
                set_rule(world.get_location(scout_name), lambda state: True)
            else:
                set_rule(world.get_location(scout_name), 
                        lambda state, reqs=required_ascents: all(state.has(ascent, player) for ascent in reqs))
        except KeyError:
            pass