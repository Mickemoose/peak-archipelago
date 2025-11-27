from typing import TYPE_CHECKING

from BaseClasses import Location, Entrance

if TYPE_CHECKING:
    from . import PeakWorld

CALDERA_LOCATIONS = [
    "Acquire Big Egg", "Acquire Egg", "Acquire Cooked Bird", "Volcanology Badge","Nomad Badge","Alpinist Badge",
]

KILN_LOCATIONS = [
    "Acquire Strange Gem", "Peak Badge", "Speed Climber Badge", "Lone Wolf Badge", "Participation Badge",
    "Survivalist Badge", "Naturalist Badge", "Leave No Trace Badge", "Balloon Badge", "Bing Bong Badge",
    "Gourmand Badge", "High Altitude Badge", "Knot Tying Badge",
]

ROOTS_LOCATIONS = [
    "Acquire Red Shroomberry", "Acquire Blue Shroomberry", "Acquire Yellow Shroomberry", 
    "Acquire Green Shroomberry", "Acquire Purple Shroomberry",
    "Acquire Mandrake", "Acquire Bounce Shroom", "Acquire Cloud Fungus", "Mycoacrobatics Badge",
    "Tread Lightly Badge",  "Undead Encounter Badge",
    "Web Security Badge", "Advanced Mycology Badge",
]

TROPICS_LOCATIONS = [
    "Acquire Red Clusterberry", "Acquire Yellow Clusterberry", "Acquire Black Clusterberry",
    "Acquire Purple Kingberry", "Acquire Yellow Kingberry", "Acquire Green Kingberry",
    "Acquire Brown Berrynana", "Acquire Blue Berrynana", "Acquire Pink Berrynana", 
    "Acquire Yellow Berrynana", "Acquire Yellow Berrynana Peel","Acquire Pink Berrynana Peel", "Acquire Honeycomb", 
    "Acquire Beehive", "Arborist Badge", "Foraging Badge","Acquire Blue Berrynana Peel",
    "Acquire Magic Bean", "Acquire Tick","Acquire Brown Berrynana Peel",
]

MESA_LOCATIONS = [
    "Acquire Cactus", "Acquire Aloe Vera", "Acquire Sunscreen", "Acquire Ancient Idol", 
    "Acquire Red Prickleberry", "Acquire Gold Prickleberry", "Acquire Scorpion", "Acquire Torch",
    "Megaentomology Badge", "Cool Cucumber Badge", "24 Karat Badge", "Astronomy Badge", 
     "Daredevil Badge", "Needlepoint Badge", "Acquire Parasol","Acquire Dynamite", "Forestry Badge",
]

ALPINE_LOCATIONS = [
    "Acquire Orange Winterberry", "Acquire Napberry", "Bundled Up Badge", "Acquire Yellow Winterberry",
     "Animal Serenading Badge", "Acquire Heat Pack", "Trailblazer Badge",
]


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
    required_ascent = world.options.ascent_count.value
    goal_type = world.options.goal.value
    progressive_stamina_enabled = world.options.progressive_stamina.value

    try:
        # Roots and Tropics require 1 Progressive Mountain
        set_rule(world.get_location("Roots Access"),
                lambda state: state.has("Progressive Mountain", player, 1))
        set_rule(world.get_location("Tropics Access"),
                lambda state: state.has("Progressive Mountain", player, 1))
        
        # Mesa and Alpine require 2 Progressive Mountain
        set_rule(world.get_location("Mesa Access"),
                lambda state: state.has("Progressive Mountain", player, 2))
        set_rule(world.get_location("Alpine Access"),
                lambda state: state.has("Progressive Mountain", player, 2))
        
        set_rule(world.get_location("Caldera Access"),
                lambda state: state.has("Progressive Mountain", player, 3))
        set_rule(world.get_location("Kiln Access"),
                lambda state: state.has("Progressive Mountain", player, 4))
    except KeyError:
        pass
    
    # All regular badge locations are always accessible
    regular_badges = [
        "Mycology Badge",
        "Endurance Badge", "Toxicology Badge", "Bouldering Badge",
        "Cooking Badge", "Plunderer Badge",
        "Esoterica Badge", "Beachcomber Badge", "Mentorship Badge",
        "Aeronautics Badge",
        "Disaster Response Badge", "Competitive Eating Badge",
        "Cryptogastronomy Badge", "Calcium Intake Badge", "Applied Esoterica Badge",  
        "Happy Camper Badge", "First Aid Badge", "Clutch Badge",
        "Emergency Preparedness Badge", "Bookworm Badge", "Resourcefulness Badge",
        "Ultimate Badge",
    ]
    
    for badge_name in regular_badges:
        try:
            set_rule(world.get_location(badge_name), lambda state: True)
        except KeyError:
            pass
    
    # Luggage locations are always accessible
    luggage_locations = [
        "Open 1 luggage", 
        "Open 5 luggage",
        "Open 10 luggage",
        "Open 15 luggage",
        "Open 20 luggage",
        "Open 25 luggage",
        "Open 30 luggage",
        "Open 35 luggage",
        "Open 40 luggage",
        "Open 45 luggage", 
        "Open 50 luggage",
        "Open 5 luggage in a single run", 
        "Open 10 luggage in a single run", 
        "Open 20 luggage in a single run",

    ]
    
    for luggage_name in luggage_locations:
        try:
            set_rule(world.get_location(luggage_name), lambda state: True)
        except KeyError:
            pass
    
    # Ascent locations require their corresponding Ascent Completed events
    roman_numerals = ["II", "III", "IV", "V", "VI", "VII", "VIII"]
    
    max_relevant_ascent = 7
    if goal_type == 0 or goal_type == 3:  # Peak Goal or Peak and Badges Goal
        max_relevant_ascent = required_ascent
    
    # Event locations for ascent completion
    for ascent_num in range(1, max_relevant_ascent + 1):
        try:
            if ascent_num in [1, 2]:
                # Ascents 1-2 only require Progressive Ascent
                set_rule(world.get_location(f"Ascent {ascent_num} Completed"),
                        lambda state, asc=ascent_num: 
                            state.has("Kiln Access", player) and
                            state.has("Progressive Ascent", player, asc))
            elif ascent_num in [3, 4, 5]:
                # Ascents 3-5 require Progressive Ascent + optionally 3 stamina bars
                if progressive_stamina_enabled:
                    set_rule(world.get_location(f"Ascent {ascent_num} Completed"),
                            lambda state, asc=ascent_num: 
                                state.has("Kiln Access", player) and
                                state.has("Progressive Ascent", player, asc) and
                                state.has("Progressive Stamina Bar", player, 3))
                else:
                    set_rule(world.get_location(f"Ascent {ascent_num} Completed"),
                            lambda state, asc=ascent_num: 
                                state.has("Kiln Access", player) and
                                state.has("Progressive Ascent", player, asc))
            elif ascent_num in [6, 7]:
                # Ascents 6-7 require Progressive Ascent + optionally 3 stamina + 4 endurance
                if progressive_stamina_enabled:
                    set_rule(world.get_location(f"Ascent {ascent_num} Completed"),
                            lambda state, asc=ascent_num: 
                                state.has("Kiln Access", player) and
                                state.has("Progressive Ascent", player, asc) and
                                state.has("Progressive Stamina Bar", player, 3) and
                                state.has("Progressive Endurance", player, 4))
                else:
                    set_rule(world.get_location(f"Ascent {ascent_num} Completed"),
                            lambda state, asc=ascent_num: 
                                state.has("Kiln Access", player) and
                                state.has("Progressive Ascent", player, asc) and
                                state.has("Progressive Endurance", player, 4))
        except KeyError:
            pass
    
    # Ascent badge locations also require Progressive Ascent to unlock that ascent
    for ascent_num in range(1, max_relevant_ascent + 1):
        roman_num = roman_numerals[ascent_num - 1]
        ascent_locations = [
            f"Beachcomber {roman_num} Badge (Ascent {ascent_num})", 
            f"Trailblazer {roman_num} Badge (Ascent {ascent_num})",
            f"Alpinist {roman_num} Badge (Ascent {ascent_num})", 
            f"Volcanology {roman_num} Badge (Ascent {ascent_num})",
            f"Nomad {roman_num} Badge (Ascent {ascent_num})",
            f"Forestry {roman_num} Badge (Ascent {ascent_num})"
        ]
        
        for ascent_name in ascent_locations:
            try:
                # Same requirements as completing the ascent
                if ascent_num in [1, 2]:
                    set_rule(world.get_location(ascent_name), 
                            lambda state, asc=ascent_num: 
                                state.has("Kiln Access", player) and
                                state.has("Progressive Ascent", player, asc))
                elif ascent_num in [3, 4, 5]:
                    set_rule(world.get_location(ascent_name), 
                            lambda state, asc=ascent_num: 
                                state.has("Kiln Access", player) and
                                state.has("Progressive Ascent", player, asc) and
                                state.has("Progressive Stamina Bar", player, 3))
                elif ascent_num in [6, 7]:
                    set_rule(world.get_location(ascent_name), 
                            lambda state, asc=ascent_num: 
                                state.has("Kiln Access", player) and
                                state.has("Progressive Ascent", player, asc) and
                                state.has("Progressive Stamina Bar", player, 3) and
                                state.has("Progressive Endurance", player, 4))
            except KeyError:
                pass
    
    # Scout sashes require completion of ALL previous ascents
    scout_sashe_requirements = {
        "Rabbit Scout sashe (Ascent 1)": ["Kiln Access"],
        "Raccoon Scout sashe (Ascent 2)": ["Ascent 1 Completed","Kiln Access"],
        "Mule Scout sashe (Ascent 3)": ["Ascent 1 Completed", "Ascent 2 Completed","Kiln Access"],
        "Kangaroo Scout sashe (Ascent 4)": ["Ascent 1 Completed", "Ascent 2 Completed", "Ascent 3 Completed","Kiln Access"],
        "Owl Scout sashe (Ascent 5)": ["Ascent 1 Completed", "Ascent 2 Completed", "Ascent 3 Completed", "Ascent 4 Completed","Kiln Access"],
        "Wolf Scout sashe (Ascent 6)": ["Ascent 1 Completed", "Ascent 2 Completed", "Ascent 3 Completed", "Ascent 4 Completed", "Ascent 5 Completed","Kiln Access"],
        "Goat Scout sashe (Ascent 7)": ["Ascent 1 Completed", "Ascent 2 Completed", "Ascent 3 Completed", "Ascent 4 Completed", "Ascent 5 Completed", "Ascent 6 Completed","Kiln Access"]
    }
    
    for scout_name, required_ascents in scout_sashe_requirements.items():
        try:
            if scout_name == "Rabbit Scout sashe (Ascent 1)":
                # Rabbit sashe just requires ability to attempt Ascent 1
                set_rule(world.get_location(scout_name), 
                        lambda state: state.has("Progressive Ascent", player, 1))
            else:
                # Extract ascent number from name
                import re
                match = re.search(r'\(Ascent (\d+)\)', scout_name)
                if match:
                    scout_ascent = int(match.group(1))
                    # Scout sashes require previous completions AND ability to attempt current ascent
                    if scout_ascent in [1, 2]:
                        set_rule(world.get_location(scout_name), 
                                lambda state, reqs=required_ascents, asc=scout_ascent: 
                                    all(state.has(ascent, player) for ascent in reqs) and
                                    state.has("Progressive Ascent", player, asc))
                    elif scout_ascent in [3, 4, 5]:
                        set_rule(world.get_location(scout_name), 
                                lambda state, reqs=required_ascents, asc=scout_ascent: 
                                    all(state.has(ascent, player) for ascent in reqs) and
                                    state.has("Progressive Ascent", player, asc) and
                                    state.has("Progressive Stamina Bar", player, 3))
                    elif scout_ascent in [6, 7]:
                        set_rule(world.get_location(scout_name), 
                                lambda state, reqs=required_ascents, asc=scout_ascent: 
                                    all(state.has(ascent, player) for ascent in reqs) and
                                    state.has("Progressive Ascent", player, asc) and
                                    state.has("Progressive Stamina Bar", player, 3) and
                                    state.has("Progressive Endurance", player, 4))
        except KeyError:
            pass
    
    # Acquire locations - most are always accessible
    acquire_locations = [
        "Acquire Rope Spool", "Acquire Rope Cannon", "Acquire Anti-Rope Spool", "Acquire Anti-Rope Cannon",
        "Acquire Chain Launcher", "Acquire Piton",
        "Acquire Balloon", "Acquire Balloon Bunch", "Acquire Scout Cannon", "Acquire Portable Stove",
        "Acquire Checkpoint Flag", "Acquire Lantern", "Acquire Flare",
        "Acquire Compass", "Acquire Pirate's Compass", "Acquire Binoculars", "Acquire Flying Disc",
        "Acquire Bandages", "Acquire First-Aid Kit", "Acquire Antidote",
        "Acquire Cure-All", "Acquire Faerie Lantern", "Acquire Scout Effigy",
        "Acquire Cursed Skull", "Acquire Pandora's Lunchbox", "Acquire Bugle of Friendship",
        "Acquire Bugle", "Acquire Remedy Fungus", "Acquire Medicinal Root", "Acquire Guidebook",
        "Acquire Shelf Shroom",  "Acquire Trail Mix",
        "Acquire Granola Bar", "Acquire Scout Cookies", "Acquire Airline Food", "Acquire Energy Drink",
        "Acquire Sports Drink", "Acquire Big Lollipop", "Acquire Button Shroom", "Acquire Bugle Shroom",
        "Acquire Cluster Shroom", "Acquire Chubby Shroom", "Acquire Conch",
        "Acquire Bing Bong", "Acquire Red Crispberry", "Acquire Green Crispberry",
        "Acquire Yellow Crispberry", "Acquire Coconut", "Acquire Coconut Half", "Acquire Blowgun",
        "Acquire Book of Bones", "Acquire Marshmallow", "Acquire Glizzy", "Acquire Rescue Claw", "Acquire Fortified Milk", "Acquire Scoutmaster's Bugle"
    ]
    
    for acquire_name in acquire_locations:
        try:
            set_rule(world.get_location(acquire_name), lambda state: True)
        except KeyError:
            pass
    
    for mesa_item in MESA_LOCATIONS:
        try:
            set_rule(world.get_location(mesa_item), 
                    lambda state: state.has("Mesa Access", player))
        except KeyError:
            pass

    for alpine_item in ALPINE_LOCATIONS:
        try:
            set_rule(world.get_location(alpine_item), 
                    lambda state: state.has("Alpine Access", player))
        except KeyError:
            pass

    for roots_item in ROOTS_LOCATIONS:
        try:
            set_rule(world.get_location(roots_item), 
                    lambda state: state.has("Roots Access", player))
        except KeyError:
            pass

    for tropics_item in TROPICS_LOCATIONS:
        try:
            set_rule(world.get_location(tropics_item), 
                    lambda state: state.has("Tropics Access", player))
        except KeyError:
            pass

    for caldera_item in CALDERA_LOCATIONS:
        try:
            set_rule(world.get_location(caldera_item), 
                    lambda state: state.has("Caldera Access", player))
        except KeyError:
            pass

    for kiln_item in KILN_LOCATIONS:
        try:
            set_rule(world.get_location(kiln_item), 
                    lambda state: state.has("Kiln Access", player))
        except KeyError:
            pass