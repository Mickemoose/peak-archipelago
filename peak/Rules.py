from typing import TYPE_CHECKING

from BaseClasses import Location, Entrance

if TYPE_CHECKING:
    from . import PeakWorld

CALDERA_LOCATIONS = [
    "Acquire Big Egg", "Acquire Egg", "Acquire Cooked Bird", "Nomad Badge", "Alpinist Badge", "Cool Cucumber Badge", "Bundled Up Badge"
]

KILN_LOCATIONS = [
    "Acquire Strange Gem", "Peak Badge", "Speed Climber Badge", "Lone Wolf Badge", "Participation Badge",
    "Survivalist Badge", "Naturalist Badge", "Leave No Trace Badge", "Balloon Badge", "Bing Bong Badge",
    "Gourmand Badge", "High Altitude Badge", "Knot Tying Badge", "24 Karat Badge", "Volcanology Badge",
]

ROOTS_LOCATIONS = [
    "Acquire Red Shroomberry", "Acquire Blue Shroomberry", "Acquire Yellow Shroomberry",
    "Acquire Green Shroomberry", "Acquire Purple Shroomberry",
    "Acquire Mandrake", "Acquire Bounce Shroom", "Acquire Cloud Fungus", "Mycoacrobatics Badge",
    "Tread Lightly Badge", "Undead Encounter Badge",
    "Web Security Badge", "Advanced Mycology Badge",
]

TROPICS_LOCATIONS = [
    "Acquire Red Clusterberry", "Acquire Yellow Clusterberry", "Acquire Black Clusterberry",
    "Acquire Purple Kingberry", "Acquire Yellow Kingberry", "Acquire Green Kingberry",
    "Acquire Brown Berrynana", "Acquire Blue Berrynana", "Acquire Pink Berrynana",
    "Acquire Yellow Berrynana", "Acquire Yellow Berrynana Peel", "Acquire Pink Berrynana Peel",
    "Acquire Honeycomb", "Acquire Beehive", "Arborist Badge", "Foraging Badge",
    "Acquire Blue Berrynana Peel", "Acquire Magic Bean", "Acquire Tick", "Acquire Brown Berrynana Peel",
    "Acquire Scorchberry",
]

MESA_LOCATIONS = [
    "Acquire Cactus", "Acquire Aloe Vera", "Acquire Sunscreen", "Acquire Ancient Idol",
    "Acquire Red Prickleberry", "Acquire Gold Prickleberry", "Acquire Scorpion", "Acquire Torch",
    "Megaentomology Badge", "Astronomy Badge",
    "Daredevil Badge", "Needlepoint Badge", "Acquire Parasol", "Acquire Dynamite", "Forestry Badge",
]

ALPINE_LOCATIONS = [
    "Acquire Orange Winterberry", "Acquire Napberry", "Acquire Yellow Winterberry",
    "Animal Serenading Badge", "Acquire Heat Pack", "Trailblazer Badge",
]


def _get_vanilla_biome_level(location_name):
    if location_name in TROPICS_LOCATIONS or location_name in ROOTS_LOCATIONS:
        return 1
    if location_name in MESA_LOCATIONS or location_name in ALPINE_LOCATIONS:
        return 2
    if location_name in CALDERA_LOCATIONS:
        return 3
    if location_name in KILN_LOCATIONS:
        return 4
    return 0


def generate_loot_biome_assignments(acquire_locations, random_source, loot_sanity_mode):
    vanilla = {loc: _get_vanilla_biome_level(loc) for loc in acquire_locations}
    if loot_sanity_mode == 0:
        return vanilla

    shuffleable_names = []
    shuffleable_levels = []
    fixed = {}

    for loc, level in vanilla.items():
        if level == 4:
            fixed[loc] = level
        else:
            shuffleable_names.append(loc)
            shuffleable_levels.append(level)

    random_source.shuffle(shuffleable_levels)

    result = dict(zip(shuffleable_names, shuffleable_levels))
    result.update(fixed)
    return result


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

    # Biome access rules
    try:
        set_rule(world.get_location("Roots Access"),
                 lambda state: state.has("Progressive Mountain", player, 1))
        set_rule(world.get_location("Tropics Access"),
                 lambda state: state.has("Progressive Mountain", player, 1))

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

    item_sanity = world.options.item_sanity.value

    # Acquire locations - when ItemSanity is on, require having received the unlock item from AP first
    # When ItemSanity is off, acquire locations only need biome access (handled below)
    acquire_item_rules = {
        "Acquire Rope Spool": "Rope Spool Unlock",
        "Acquire Rope Cannon": "Rope Cannon Unlock",
        "Acquire Anti-Rope Spool": "Anti-Rope Spool Unlock",
        "Acquire Anti-Rope Cannon": "Anti-Rope Cannon Unlock",
        "Acquire Chain Launcher": "Chain Launcher Unlock",
        "Acquire Piton": "Piton Unlock",
        "Acquire Rescue Claw": "Rescue Claw Unlock",
        "Acquire Magic Bean": "Magic Bean Unlock",
        "Acquire Parasol": "Parasol Unlock",
        "Acquire Balloon": "Balloon Unlock",
        "Acquire Balloon Bunch": "Balloon Bunch Unlock",
        "Acquire Scout Cannon": "Scout Cannon Unlock",
        "Acquire Flying Disc": "Flying Disc Unlock",
        "Acquire Guidebook": "Guidebook Unlock",
        "Acquire Portable Stove": "Portable Stove Unlock",
        "Acquire Checkpoint Flag": "Checkpoint Flag Unlock",
        "Acquire Lantern": "Lantern Unlock",
        "Acquire Flare": "Flare Unlock",
        "Acquire Torch": "Torch Unlock",
        "Acquire Faerie Lantern": "Faerie Lantern Unlock",
        "Acquire Blowgun": "Blowgun Unlock",
        "Acquire Cactus": "Cactus Unlock",
        "Acquire Compass": "Compass Unlock",
        "Acquire Pirate's Compass": "Pirate's Compass Unlock",
        "Acquire Binoculars": "Binoculars Unlock",
        "Acquire Bandages": "Bandages Unlock",
        "Acquire First-Aid Kit": "First-Aid Kit Unlock",
        "Acquire Antidote": "Antidote Unlock",
        "Acquire Heat Pack": "Heat Pack Unlock",
        "Acquire Cure-All": "Cure-All Unlock",
        "Acquire Remedy Fungus": "Remedy Fungus Unlock",
        "Acquire Medicinal Root": "Medicinal Root Unlock",
        "Acquire Aloe Vera": "Aloe Vera Unlock",
        "Acquire Sunscreen": "Sunscreen Unlock",
        "Acquire Marshmallow": "Marshmallow Unlock",
        "Acquire Glizzy": "Glizzy Unlock",
        "Acquire Fortified Milk": "Fortified Milk Unlock",
        "Acquire Scout Effigy": "Scout Effigy Unlock",
        "Acquire Cursed Skull": "Cursed Skull Unlock",
        "Acquire Pandora's Lunchbox": "Pandora's Lunchbox Unlock",
        "Acquire Ancient Idol": "Ancient Idol Unlock",
        "Acquire Strange Gem": "Strange Gem Unlock",
        "Acquire Book of Bones": "Book of Bones Unlock",
        "Acquire Cloud Fungus": "Cloud Fungus Unlock",
        "Acquire Bugle of Friendship": "Bugle of Friendship Unlock",
        "Acquire Bugle": "Bugle Unlock",
        "Acquire Shelf Shroom": "Shelf Shroom Unlock",
        "Acquire Bounce Shroom": "Bounce Shroom Unlock",
        "Acquire Button Shroom": "Button Shroom Unlock",
        "Acquire Bugle Shroom": "Bugle Shroom Unlock",
        "Acquire Cluster Shroom": "Cluster Shroom Unlock",
        "Acquire Chubby Shroom": "Chubby Shroom Unlock",
        "Acquire Trail Mix": "Trail Mix Unlock",
        "Acquire Granola Bar": "Granola Bar Unlock",
        "Acquire Scout Cookies": "Scout Cookies Unlock",
        "Acquire Airline Food": "Airline Food Unlock",
        "Acquire Energy Drink": "Energy Drink Unlock",
        "Acquire Sports Drink": "Sports Drink Unlock",
        "Acquire Big Lollipop": "Big Lollipop Unlock",
        "Acquire Big Egg": "Big Egg Unlock",
        "Acquire Egg": "Egg Unlock",
        "Acquire Cooked Bird": "Cooked Bird Unlock",
        "Acquire Honeycomb": "Honeycomb Unlock",
        "Acquire Beehive": "Beehive Unlock",
        "Acquire Scorpion": "Scorpion Unlock",
        "Acquire Tick": "Tick Unlock",
        "Acquire Conch": "Conch Unlock",
        "Acquire Dynamite": "Dynamite Unlock",
        "Acquire Bing Bong": "Bing Bong Unlock",
        "Acquire Mandrake": "Mandrake Unlock",
        "Acquire Red Crispberry": "Red Crispberry Unlock",
        "Acquire Green Crispberry": "Green Crispberry Unlock",
        "Acquire Yellow Crispberry": "Yellow Crispberry Unlock",
        "Acquire Coconut": "Coconut Unlock",
        "Acquire Coconut Half": "Coconut Half Unlock",
        "Acquire Brown Berrynana": "Brown Berrynana Unlock",
        "Acquire Blue Berrynana": "Blue Berrynana Unlock",
        "Acquire Pink Berrynana": "Pink Berrynana Unlock",
        "Acquire Yellow Berrynana": "Yellow Berrynana Unlock",
        "Acquire Orange Winterberry": "Orange Winterberry Unlock",
        "Acquire Yellow Winterberry": "Yellow Winterberry Unlock",
        "Acquire Red Prickleberry": "Red Prickleberry Unlock",
        "Acquire Gold Prickleberry": "Gold Prickleberry Unlock",
        "Acquire Red Shroomberry": "Red Shroomberry Unlock",
        "Acquire Blue Shroomberry": "Blue Shroomberry Unlock",
        "Acquire Green Shroomberry": "Green Shroomberry Unlock",
        "Acquire Yellow Shroomberry": "Yellow Shroomberry Unlock",
        "Acquire Purple Shroomberry": "Purple Shroomberry Unlock",
        "Acquire Purple Kingberry": "Purple Kingberry Unlock",
        "Acquire Yellow Kingberry": "Yellow Kingberry Unlock",
        "Acquire Green Kingberry": "Green Kingberry Unlock",
        "Acquire Napberry": "Napberry Unlock",
        "Acquire Black Clusterberry": "Black Clusterberry Unlock",
        "Acquire Red Clusterberry": "Red Clusterberry Unlock",
        "Acquire Yellow Clusterberry": "Yellow Clusterberry Unlock",
        "Acquire Scorchberry": "Scorchberry Unlock",
        "Acquire Scoutmaster's Bugle": "Scoutmaster's Bugle Unlock",
        "Acquire Yellow Berrynana Peel": "Yellow Berrynana Unlock",
        "Acquire Pink Berrynana Peel": "Pink Berrynana Unlock",
        "Acquire Blue Berrynana Peel": "Blue Berrynana Unlock",
        "Acquire Brown Berrynana Peel": "Brown Berrynana Unlock",
    }

    # Generate loot biome assignments (shuffled when loot sanity is on)
    loot_sanity_mode = world.options.loot_sanity.value
    loot_biome = generate_loot_biome_assignments(
        acquire_item_rules.keys(), world.random, loot_sanity_mode
    )
    world.loot_biome_assignments = loot_biome

    # Apply acquire item rules using the (possibly shuffled) biome assignments
    for location_name, required_item in acquire_item_rules.items():
        try:
            biome_level = loot_biome.get(location_name, 0)
            if item_sanity:
                if biome_level == 0:
                    set_rule(world.get_location(location_name),
                             lambda state, item=required_item:
                             state.has(item, player))
                else:
                    set_rule(world.get_location(location_name),
                             lambda state, item=required_item, lvl=biome_level:
                             state.has(item, player) and
                             state.has("Progressive Mountain", player, lvl))
            else:
                if biome_level == 0:
                    set_rule(world.get_location(location_name), lambda state: True)
                else:
                    set_rule(world.get_location(location_name),
                             lambda state, lvl=biome_level:
                             state.has("Progressive Mountain", player, lvl))
        except KeyError:
            pass

    # Build unlock-to-biome-level mapping from loot assignments
    # For unlocks that map to multiple acquire locations, use the minimum biome level
    unlock_to_biome = {}
    for acquire_loc, unlock_name in acquire_item_rules.items():
        level = loot_biome.get(acquire_loc, 0)
        if unlock_name not in unlock_to_biome or level < unlock_to_biome[unlock_name]:
            unlock_to_biome[unlock_name] = level

    def _biome_rule(lvl):
        if lvl == 0:
            return lambda state: True
        return lambda state, l=lvl: state.has("Progressive Mountain", player, l)

    def _unlock_and_biome_rule(unlock, lvl):
        if lvl == 0:
            return lambda state, u=unlock: state.has(u, player)
        return lambda state, u=unlock, l=lvl: state.has(u, player) and state.has("Progressive Mountain", player, l)

    # All regular badge locations are always accessible
    regular_badges = [
        "Mycology Badge",
        "Endurance Badge", "Toxicology Badge", "Bouldering Badge",
        "Cooking Badge", "Plunderer Badge",
        "Esoterica Badge", "Beachcomber Badge", "Mentorship Badge",
        "Disaster Response Badge", "Competitive Eating Badge",
        "Cryptogastronomy Badge", "Calcium Intake Badge", "Applied Esoterica Badge",
        "Happy Camper Badge", "First Aid Badge", "Clutch Badge",
        "Emergency Preparedness Badge", "Bookworm Badge", "Resourcefulness Badge",
        "Ultimate Badge",
    ]

    special_badges = {"Mycology Badge", "Applied Esoterica Badge", "Bouldering Badge",
                      "Calcium Intake Badge", "Competitive Eating Badge",
                      "Cryptogastronomy Badge", "Disaster Response Badge", "Esoterica Badge",
                      "Toxicology Badge", "Ultimate Badge", "Beachcomber Badge",
                      "Plunderer Badge"}
    for badge_name in [b for b in regular_badges if b not in special_badges]:
        try:
            set_rule(world.get_location(badge_name), lambda state: True)
        except KeyError:
            pass

    # Mycology Badge — ALL of 4 shroom unlocks; biome = max level among them
    try:
        mycology_unlocks = ["Bugle Shroom Unlock", "Button Shroom Unlock",
                            "Chubby Shroom Unlock", "Cluster Shroom Unlock"]
        mycology_biome = max(unlock_to_biome.get(u, 0) for u in mycology_unlocks)
        if item_sanity:
            set_rule(world.get_location("Mycology Badge"),
                     lambda state, lvl=mycology_biome:
                     state.has("Bugle Shroom Unlock", player) and
                     state.has("Button Shroom Unlock", player) and
                     state.has("Chubby Shroom Unlock", player) and
                     state.has("Cluster Shroom Unlock", player) and
                     (lvl == 0 or state.has("Progressive Mountain", player, lvl)))
        else:
            set_rule(world.get_location("Mycology Badge"), _biome_rule(mycology_biome))
    except KeyError:
        pass

    # Aeronautics Badge — ANY of Balloon or Balloon Bunch; biome = min level
    try:
        aero_unlocks = ["Balloon Unlock", "Balloon Bunch Unlock"]
        aero_biome = min(unlock_to_biome.get(u, 0) for u in aero_unlocks)
        if item_sanity:
            set_rule(world.get_location("Aeronautics Badge"),
                     lambda state, lvl=aero_biome:
                     (state.has("Balloon Unlock", player) or
                      state.has("Balloon Bunch Unlock", player)) and
                     (lvl == 0 or state.has("Progressive Mountain", player, lvl)))
        else:
            set_rule(world.get_location("Aeronautics Badge"), _biome_rule(aero_biome))
    except KeyError:
        pass

    # Applied Esoterica Badge — Book of Bones
    try:
        app_eso_biome = unlock_to_biome.get("Book of Bones Unlock", 0)
        if item_sanity:
            set_rule(world.get_location("Applied Esoterica Badge"),
                     _unlock_and_biome_rule("Book of Bones Unlock", app_eso_biome))
        else:
            set_rule(world.get_location("Applied Esoterica Badge"), _biome_rule(app_eso_biome))
    except KeyError:
        pass

    # Bouldering Badge — Piton
    try:
        boulder_biome = unlock_to_biome.get("Piton Unlock", 0)
        if item_sanity:
            set_rule(world.get_location("Bouldering Badge"),
                     _unlock_and_biome_rule("Piton Unlock", boulder_biome))
        else:
            set_rule(world.get_location("Bouldering Badge"), _biome_rule(boulder_biome))
    except KeyError:
        pass

    # Calcium Intake Badge — Fortified Milk
    try:
        calcium_biome = unlock_to_biome.get("Fortified Milk Unlock", 0)
        if item_sanity:
            set_rule(world.get_location("Calcium Intake Badge"),
                     _unlock_and_biome_rule("Fortified Milk Unlock", calcium_biome))
        else:
            set_rule(world.get_location("Calcium Intake Badge"), _biome_rule(calcium_biome))
    except KeyError:
        pass

    # Competitive Eating Badge — Glizzy
    try:
        comp_biome = unlock_to_biome.get("Glizzy Unlock", 0)
        if item_sanity:
            set_rule(world.get_location("Competitive Eating Badge"),
                     _unlock_and_biome_rule("Glizzy Unlock", comp_biome))
        else:
            set_rule(world.get_location("Competitive Eating Badge"), _biome_rule(comp_biome))
    except KeyError:
        pass

    # Cryptogastronomy Badge — Mandrake
    try:
        crypto_biome = unlock_to_biome.get("Mandrake Unlock", 0)
        if item_sanity:
            set_rule(world.get_location("Cryptogastronomy Badge"),
                     _unlock_and_biome_rule("Mandrake Unlock", crypto_biome))
        else:
            set_rule(world.get_location("Cryptogastronomy Badge"), _biome_rule(crypto_biome))
    except KeyError:
        pass

    # Disaster Response Badge — Rescue Claw
    try:
        disaster_biome = unlock_to_biome.get("Rescue Claw Unlock", 0)
        if item_sanity:
            set_rule(world.get_location("Disaster Response Badge"),
                     _unlock_and_biome_rule("Rescue Claw Unlock", disaster_biome))
        else:
            set_rule(world.get_location("Disaster Response Badge"), _biome_rule(disaster_biome))
    except KeyError:
        pass

    # Esoterica Badge — ANY of 11 items; biome = min level among them
    try:
        eso_unlocks = [
            "Ancient Idol Unlock", "Anti-Rope Cannon Unlock", "Anti-Rope Spool Unlock",
            "Bugle of Friendship Unlock", "Cure-All Unlock", "Cursed Skull Unlock",
            "Faerie Lantern Unlock", "Pandora's Lunchbox Unlock", "Scout Effigy Unlock",
            "Scoutmaster's Bugle Unlock", "Book of Bones Unlock",
        ]
        eso_biome = min(unlock_to_biome.get(u, 0) for u in eso_unlocks)
        if item_sanity:
            set_rule(world.get_location("Esoterica Badge"),
                     lambda state, lvl=eso_biome:
                     (state.has("Ancient Idol Unlock", player) or
                      state.has("Anti-Rope Cannon Unlock", player) or
                      state.has("Anti-Rope Spool Unlock", player) or
                      state.has("Bugle of Friendship Unlock", player) or
                      state.has("Cure-All Unlock", player) or
                      state.has("Cursed Skull Unlock", player) or
                      state.has("Faerie Lantern Unlock", player) or
                      state.has("Pandora's Lunchbox Unlock", player) or
                      state.has("Scout Effigy Unlock", player) or
                      state.has("Scoutmaster's Bugle Unlock", player) or
                      state.has("Book of Bones Unlock", player)) and
                     (lvl == 0 or state.has("Progressive Mountain", player, lvl)))
        else:
            set_rule(world.get_location("Esoterica Badge"), _biome_rule(eso_biome))
    except KeyError:
        pass

    # Plunderer Badge — requires 1 Progressive Mountain
    try:
        set_rule(world.get_location("Plunderer Badge"),
                 lambda state: state.has("Progressive Mountain", player, 1))
    except KeyError:
        pass

    # Beachcomber Badge — requires 1 Progressive Mountain
    try:
        set_rule(world.get_location("Beachcomber Badge"),
                 lambda state: state.has("Progressive Mountain", player, 1))
    except KeyError:
        pass

    # Ultimate Badge — Flying Disc
    try:
        ult_biome = unlock_to_biome.get("Flying Disc Unlock", 0)
        if item_sanity:
            set_rule(world.get_location("Ultimate Badge"),
                     _unlock_and_biome_rule("Flying Disc Unlock", ult_biome))
        else:
            set_rule(world.get_location("Ultimate Badge"), _biome_rule(ult_biome))
    except KeyError:
        pass

    # Toxicology Badge — ANY of Antidote, Cure-All, First-Aid Kit, Medicinal Root
    try:
        tox_unlocks = ["Antidote Unlock", "Cure-All Unlock",
                       "First-Aid Kit Unlock", "Medicinal Root Unlock"]
        tox_biome = min(unlock_to_biome.get(u, 0) for u in tox_unlocks)
        if item_sanity:
            set_rule(world.get_location("Toxicology Badge"),
                     lambda state, lvl=tox_biome:
                     (state.has("Antidote Unlock", player) or
                      state.has("Cure-All Unlock", player) or
                      state.has("First-Aid Kit Unlock", player) or
                      state.has("Medicinal Root Unlock", player)) and
                     (lvl == 0 or state.has("Progressive Mountain", player, lvl)))
        else:
            set_rule(world.get_location("Toxicology Badge"), _biome_rule(tox_biome))
    except KeyError:
        pass

    luggage_no_gate = [
        "Open 1 luggage",
        "Open 5 luggage",
        "Open 10 luggage",
    ]
    luggage_mountain_gates = {
        1: ["Open 15 luggage", "Open 20 luggage", "Open 25 luggage", "Open 30 luggage"],
        2: ["Open 35 luggage", "Open 40 luggage", "Open 45 luggage", "Open 50 luggage"],
        3: ["Open 55 luggage", "Open 60 luggage", "Open 65 luggage", "Open 70 luggage"],
        4: ["Open 75 luggage", "Open 80 luggage", "Open 85 luggage", "Open 90 luggage",
            "Open 95 luggage", "Open 100 luggage"],
    }
    single_run_no_gate = [
        "Open 2 luggage in a single run",
        "Open 5 luggage in a single run",
        "Open 10 luggage in a single run",
    ]
    single_run_mountain_gates = {
        1: ["Open 15 luggage in a single run"],
        2: ["Open 20 luggage in a single run"],
    }

    for luggage_name in luggage_no_gate + single_run_no_gate:
        try:
            set_rule(world.get_location(luggage_name), lambda state: True)
        except KeyError:
            pass

    for mountains_needed, locations in {**luggage_mountain_gates, **single_run_mountain_gates}.items():
        for luggage_name in locations:
            try:
                set_rule(world.get_location(luggage_name),
                         lambda state, m=mountains_needed: state.has("Progressive Mountain", player, m))
            except KeyError:
                pass

    # Biome-locked badges (not in acquire_item_rules)
    # Badges with item requirements use the loot shuffle to determine biome access.
    # Badges without item requirements keep their vanilla biome access.
    for mesa_item in MESA_LOCATIONS:
        if mesa_item not in acquire_item_rules:
            try:
                if mesa_item == "Astronomy Badge" and item_sanity:
                    astro_biome = unlock_to_biome.get("Binoculars Unlock", 0)
                    set_rule(world.get_location(mesa_item),
                             _unlock_and_biome_rule("Binoculars Unlock", astro_biome))
                elif mesa_item == "Daredevil Badge" and item_sanity:
                    dare_biome = unlock_to_biome.get("Scout Cannon Unlock", 0)
                    set_rule(world.get_location(mesa_item),
                             _unlock_and_biome_rule("Scout Cannon Unlock", dare_biome))
                elif mesa_item == "Needlepoint Badge" and item_sanity:
                    needle_biome = unlock_to_biome.get("Cactus Unlock", 0)
                    set_rule(world.get_location(mesa_item),
                             _unlock_and_biome_rule("Cactus Unlock", needle_biome))
                elif mesa_item == "Needlepoint Badge":
                    needle_biome = unlock_to_biome.get("Cactus Unlock", 0)
                    set_rule(world.get_location(mesa_item), _biome_rule(needle_biome))
                else:
                    set_rule(world.get_location(mesa_item),
                             lambda state: state.has("Mesa Access", player))
            except KeyError:
                pass

    for alpine_item in ALPINE_LOCATIONS:
        if alpine_item not in acquire_item_rules:
            try:
                if alpine_item == "Animal Serenading Badge" and item_sanity:
                    serenade_unlocks = ["Bugle Unlock", "Scoutmaster's Bugle Unlock",
                                        "Bugle of Friendship Unlock"]
                    serenade_biome = min(unlock_to_biome.get(u, 0) for u in serenade_unlocks)
                    set_rule(world.get_location(alpine_item),
                             lambda state, lvl=serenade_biome:
                             (state.has("Bugle Unlock", player) or
                              state.has("Scoutmaster's Bugle Unlock", player) or
                              state.has("Bugle of Friendship Unlock", player)) and
                             (lvl == 0 or state.has("Progressive Mountain", player, lvl)))
                else:
                    set_rule(world.get_location(alpine_item),
                             lambda state: state.has("Alpine Access", player))
            except KeyError:
                pass

    for roots_item in ROOTS_LOCATIONS:
        if roots_item not in acquire_item_rules:
            try:
                if roots_item == "Advanced Mycology Badge" and item_sanity:
                    adv_myc_unlocks = ["Red Shroomberry Unlock", "Blue Shroomberry Unlock",
                                       "Green Shroomberry Unlock", "Yellow Shroomberry Unlock",
                                       "Purple Shroomberry Unlock"]
                    adv_myc_biome = max(unlock_to_biome.get(u, 0) for u in adv_myc_unlocks)
                    set_rule(world.get_location(roots_item),
                             lambda state, lvl=adv_myc_biome:
                             state.has("Red Shroomberry Unlock", player) and
                             state.has("Blue Shroomberry Unlock", player) and
                             state.has("Green Shroomberry Unlock", player) and
                             state.has("Yellow Shroomberry Unlock", player) and
                             state.has("Purple Shroomberry Unlock", player) and
                             (lvl == 0 or state.has("Progressive Mountain", player, lvl)))
                else:
                    set_rule(world.get_location(roots_item),
                             lambda state: state.has("Roots Access", player))
            except KeyError:
                pass

    berry_unlocks = [
        "Red Crispberry Unlock", "Green Crispberry Unlock", "Yellow Crispberry Unlock",
        "Brown Berrynana Unlock", "Blue Berrynana Unlock", "Pink Berrynana Unlock", "Yellow Berrynana Unlock",
        "Orange Winterberry Unlock", "Yellow Winterberry Unlock",
        "Red Prickleberry Unlock", "Gold Prickleberry Unlock",
        "Red Shroomberry Unlock", "Blue Shroomberry Unlock", "Green Shroomberry Unlock",
        "Yellow Shroomberry Unlock", "Purple Shroomberry Unlock",
        "Purple Kingberry Unlock", "Yellow Kingberry Unlock", "Green Kingberry Unlock",
        "Napberry Unlock",
        "Black Clusterberry Unlock", "Red Clusterberry Unlock", "Yellow Clusterberry Unlock",
    ]

    # Foraging Badge — need 5 berries; biome = 5th-lowest biome level among all berries
    berry_biome_levels = sorted(unlock_to_biome.get(b, 0) for b in berry_unlocks)
    foraging_biome = berry_biome_levels[4] if len(berry_biome_levels) >= 5 else 0

    for tropics_item in TROPICS_LOCATIONS:
        if tropics_item not in acquire_item_rules:
            try:
                if tropics_item == "Foraging Badge" and item_sanity:
                    set_rule(world.get_location(tropics_item),
                             lambda state, lvl=foraging_biome:
                             sum(1 for b in berry_unlocks if state.has(b, player)) >= 5 and
                             (lvl == 0 or state.has("Progressive Mountain", player, lvl)))
                elif tropics_item == "Foraging Badge":
                    set_rule(world.get_location(tropics_item), _biome_rule(foraging_biome))
                else:
                    set_rule(world.get_location(tropics_item),
                             lambda state: state.has("Tropics Access", player))
            except KeyError:
                pass

    for caldera_item in CALDERA_LOCATIONS:
        if caldera_item not in acquire_item_rules:
            try:
                set_rule(world.get_location(caldera_item),
                         lambda state: state.has("Caldera Access", player))
            except KeyError:
                pass

    for kiln_item in KILN_LOCATIONS:
        if kiln_item not in acquire_item_rules:
            try:
                if kiln_item == "24 Karat Badge" and item_sanity:
                    karat_biome = unlock_to_biome.get("Ancient Idol Unlock", 0)
                    set_rule(world.get_location(kiln_item),
                             _unlock_and_biome_rule("Ancient Idol Unlock", karat_biome))
                elif kiln_item == "Bing Bong Badge" and item_sanity:
                    bing_biome = unlock_to_biome.get("Bing Bong Unlock", 0)
                    set_rule(world.get_location(kiln_item),
                             _unlock_and_biome_rule("Bing Bong Unlock", bing_biome))
                elif kiln_item == "Knot Tying Badge" and item_sanity:
                    knot_unlocks = ["Rope Spool Unlock", "Anti-Rope Spool Unlock",
                                    "Rope Cannon Unlock", "Anti-Rope Cannon Unlock"]
                    knot_biome = min(unlock_to_biome.get(u, 0) for u in knot_unlocks)
                    set_rule(world.get_location(kiln_item),
                             lambda state, lvl=knot_biome:
                             (state.has("Rope Spool Unlock", player) or
                              state.has("Anti-Rope Spool Unlock", player) or
                              state.has("Rope Cannon Unlock", player) or
                              state.has("Anti-Rope Cannon Unlock", player)) and
                             (lvl == 0 or state.has("Progressive Mountain", player, lvl)))
                elif kiln_item == "Knot Tying Badge":
                    knot_unlocks = ["Rope Spool Unlock", "Anti-Rope Spool Unlock",
                                    "Rope Cannon Unlock", "Anti-Rope Cannon Unlock"]
                    knot_biome = min(unlock_to_biome.get(u, 0) for u in knot_unlocks)
                    set_rule(world.get_location(kiln_item), _biome_rule(knot_biome))
                elif kiln_item == "Gourmand Badge" and item_sanity:
                    gourmand_unlocks = ["Coconut Unlock", "Honeycomb Unlock",
                                        "Yellow Winterberry Unlock", "Egg Unlock"]
                    gourmand_biome = max(unlock_to_biome.get(u, 0) for u in gourmand_unlocks)
                    set_rule(world.get_location(kiln_item),
                             lambda state, lvl=gourmand_biome:
                             state.has("Coconut Unlock", player) and
                             state.has("Honeycomb Unlock", player) and
                             state.has("Yellow Winterberry Unlock", player) and
                             state.has("Egg Unlock", player) and
                             (lvl == 0 or state.has("Progressive Mountain", player, lvl)))
                elif kiln_item == "Gourmand Badge":
                    gourmand_unlocks = ["Coconut Unlock", "Honeycomb Unlock",
                                        "Yellow Winterberry Unlock", "Egg Unlock"]
                    gourmand_biome = max(unlock_to_biome.get(u, 0) for u in gourmand_unlocks)
                    set_rule(world.get_location(kiln_item), _biome_rule(gourmand_biome))
                else:
                    set_rule(world.get_location(kiln_item),
                             lambda state: state.has("Kiln Access", player))
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
                set_rule(world.get_location(f"Ascent {ascent_num} Completed"),
                         lambda state, asc=ascent_num:
                         state.has("Kiln Access", player) and
                         state.has("Progressive Ascent", player, asc))
            elif ascent_num in [3, 4, 5]:
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

    # Ascent badge locations require Progressive Ascent
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
        "Raccoon Scout sashe (Ascent 2)": ["Ascent 1 Completed", "Kiln Access"],
        "Mule Scout sashe (Ascent 3)": ["Ascent 1 Completed", "Ascent 2 Completed", "Kiln Access"],
        "Kangaroo Scout sashe (Ascent 4)": ["Ascent 1 Completed", "Ascent 2 Completed", "Ascent 3 Completed",
                                            "Kiln Access"],
        "Owl Scout sashe (Ascent 5)": ["Ascent 1 Completed", "Ascent 2 Completed", "Ascent 3 Completed",
                                       "Ascent 4 Completed", "Kiln Access"],
        "Wolf Scout sashe (Ascent 6)": ["Ascent 1 Completed", "Ascent 2 Completed", "Ascent 3 Completed",
                                        "Ascent 4 Completed", "Ascent 5 Completed", "Kiln Access"],
        "Goat Scout sashe (Ascent 7)": ["Ascent 1 Completed", "Ascent 2 Completed", "Ascent 3 Completed",
                                        "Ascent 4 Completed", "Ascent 5 Completed", "Ascent 6 Completed", "Kiln Access"]
    }

    for scout_name, required_ascents in scout_sashe_requirements.items():
        try:
            if scout_name == "Rabbit Scout sashe (Ascent 1)":
                set_rule(world.get_location(scout_name),
                         lambda state: state.has("Progressive Ascent", player, 1) and
                         state.has("Progressive Mountain", player, 4))
            else:
                import re
                match = re.search(r'\(Ascent (\d+)\)', scout_name)
                if match:
                    scout_ascent = int(match.group(1))
                    if scout_ascent in [1, 2]:
                        set_rule(world.get_location(scout_name),
                                 lambda state, reqs=required_ascents, asc=scout_ascent:
                                 all(state.has(ascent, player) for ascent in reqs) and
                                 state.has("Progressive Ascent", player, asc) and
                                 state.has("Progressive Mountain", player, 4))
                    elif scout_ascent in [3, 4, 5]:
                        set_rule(world.get_location(scout_name),
                                 lambda state, reqs=required_ascents, asc=scout_ascent:
                                 all(state.has(ascent, player) for ascent in reqs) and
                                 state.has("Progressive Ascent", player, asc) and
                                 state.has("Progressive Mountain", player, 4) and
                                 state.has("Progressive Stamina Bar", player, 3))
                    elif scout_ascent in [6, 7]:
                        set_rule(world.get_location(scout_name),
                                 lambda state, reqs=required_ascents, asc=scout_ascent:
                                 all(state.has(ascent, player) for ascent in reqs) and
                                 state.has("Progressive Ascent", player, asc) and
                                 state.has("Progressive Mountain", player, 4) and
                                 state.has("Progressive Stamina Bar", player, 3) and
                                 state.has("Progressive Endurance", player, 4))
        except KeyError:
            pass