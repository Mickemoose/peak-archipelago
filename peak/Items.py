from BaseClasses import Item, ItemClassification

class PeakItem(Item):
    game = "PEAK"
    
    def __init__(self, name, classification, code, player):
        super().__init__(name, classification, code, player)
        self.code = code

    def __repr__(self):
        return "<PeakItem {} (ID: {})>".format(self.name, self.code)


ITEMS = {
    #region Useful Items
    "Bounce Fungus": (76000, ItemClassification.useful),
    "Airline Food": (76001, ItemClassification.useful),
    "Antidote": (76002, ItemClassification.useful),
    "Big Lollipop": (76004, ItemClassification.useful),
    "Chain Launcher": (76007, ItemClassification.useful),
    "Cure-All": (76008, ItemClassification.useful),
    "Pandora's Lunchbox": (76013, ItemClassification.useful),
    "Piton": (76014, ItemClassification.useful),
    "Remedy Fungus": (76016, ItemClassification.useful),
    "Sports Drink": (76017, ItemClassification.useful),
    #"Clear All Effects": (76028, ItemClassification.useful),
    "Speed Upgrade": (76031, ItemClassification.useful),
    #endregion
    
    #region Filler Items
    "Bandages": (76003, ItemClassification.filler),
    "Granola Bar": (76010, ItemClassification.filler),
    "Green Crispberry": (76011, ItemClassification.filler),
    "Lantern": (76012, ItemClassification.filler),
    "Red Crispberry": (76015, ItemClassification.filler),
    "Yellow Crispberry": (76018, ItemClassification.filler),
    #"Spawn Small Luggage": (76026, ItemClassification.filler),
    #endregion
    
    #region Trap Items
    "Blue Berrynana Peel": (76005, ItemClassification.trap),
    "Coconut": (76006, ItemClassification.trap),
    "Dynamite": (76009, ItemClassification.trap),
    "Spawn Bee Swarm": (76027, ItemClassification.trap),
    "Destroy Held Item": (76029, ItemClassification.trap),
    #"Spawn Lightning": (76030, ItemClassification.trap),
    "Minor Poison Trap": (76032, ItemClassification.trap),
    "Poison Trap": (76033, ItemClassification.trap),
    "Deadly Poison Trap": (76034, ItemClassification.trap),
    "Tornado Trap": (76035, ItemClassification.trap),
    "Nap Time Trap": (76036, ItemClassification.trap),
    "Balloon Trap": (76037, ItemClassification.trap),
    "Hungry Hungry Camper Trap": (76038, ItemClassification.trap),
    #endregion
    
    #region Progression Items
    "Ascent 1 Unlock": (76019, ItemClassification.progression),
    "Ascent 2 Unlock": (76020, ItemClassification.progression),
    "Ascent 3 Unlock": (76021, ItemClassification.progression),
    "Ascent 4 Unlock": (76022, ItemClassification.progression),
    "Ascent 5 Unlock": (76023, ItemClassification.progression),
    "Ascent 6 Unlock": (76024, ItemClassification.progression),
    "Ascent 7 Unlock": (76025, ItemClassification.progression),
    "Progressive Stamina Bar": (77080, ItemClassification.useful),
    #endregion
    
    #region Game Items
    "Rope Spool": (77000, ItemClassification.useful),
    "Rope Cannon": (77001, ItemClassification.useful),
    "Anti-Rope Spool": (77002, ItemClassification.useful),
    "Anti-Rope Cannon": (77003, ItemClassification.useful),
    "Piton": (77005, ItemClassification.useful),
    "Magic Bean": (77006, ItemClassification.useful),
    "Parasol": (77007, ItemClassification.useful),
    "Balloon": (77008, ItemClassification.useful),
    "Balloon Bunch": (77009, ItemClassification.useful),
    "Scout Cannon": (77010, ItemClassification.useful),
    "Portable Stove": (77011, ItemClassification.useful),
    "Campfire": (77012, ItemClassification.useful),
    "Lantern": (77013, ItemClassification.useful),
    "Flare": (77014, ItemClassification.useful),
    "Torch": (77015, ItemClassification.useful),
    "Cactus": (77016, ItemClassification.useful),
    "Compass": (77017, ItemClassification.useful),
    "Pirate's Compass": (77018, ItemClassification.useful),
    "Binoculars": (77019, ItemClassification.useful),
    "Flying Disc": (77020, ItemClassification.useful),
    "Bandages": (77021, ItemClassification.filler),
    "First-Aid Kit": (77022, ItemClassification.useful),
    "Antidote": (77023, ItemClassification.useful),
    "Heat Pack": (77024, ItemClassification.useful),
    "Cure-All": (77025, ItemClassification.useful),
    "Faerie Lantern": (77026, ItemClassification.useful),
    "Remedy Fungus": (77027, ItemClassification.useful),
    "Aloe Vera": (77028, ItemClassification.useful),
    "Sunscreen": (77029, ItemClassification.useful),
    "Scout Effigy": (77030, ItemClassification.useful),
    "Cursed Skull": (77031, ItemClassification.trap),
    "Pandora's Lunchbox": (77032, ItemClassification.useful),
    "Ancient Idol": (77033, ItemClassification.useful),
    "Bugle of Friendship": (77034, ItemClassification.useful),
    "Bugle": (77035, ItemClassification.useful),
    "Medicinal Root": (77036, ItemClassification.useful),
    "Shelf Shroom": (77037, ItemClassification.useful),
    "Bounce Shroom": (77038, ItemClassification.useful),
    "Trail Mix": (77039, ItemClassification.filler),
    "Granola Bar": (77040, ItemClassification.filler),
    "Scout Cookies": (77041, ItemClassification.filler),
    "Airline Food": (77042, ItemClassification.useful),
    "Energy Drink": (77043, ItemClassification.useful),
    "Sports Drink": (77044, ItemClassification.useful),
    "Big Lollipop": (77045, ItemClassification.useful),
    "Button Shroom": (77046, ItemClassification.useful),
    "Bugle Shroom": (77047, ItemClassification.useful),
    "Cluster Shroom": (77048, ItemClassification.useful),
    "Chubby Shroom": (77049, ItemClassification.useful),
    "Conch": (77050, ItemClassification.useful),
    "Banana Peel": (77051, ItemClassification.trap),
    "Dynamite": (77052, ItemClassification.trap),
    "Bing Bong": (77053, ItemClassification.useful),
    "Red Crispberry": (77054, ItemClassification.filler),
    "Green Crispberry": (77055, ItemClassification.filler),
    "Yellow Crispberry": (77056, ItemClassification.filler),
    "Coconut": (77057, ItemClassification.trap),
    "Coconut Half": (77058, ItemClassification.trap),
    "Brown Berrynana": (77059, ItemClassification.filler),
    "Blue Berrynana": (77060, ItemClassification.trap),
    "Pink Berrynana": (77061, ItemClassification.filler),
    "Yellow Berrynana": (77062, ItemClassification.filler),
    "Orange Winterberry": (77063, ItemClassification.useful),
    "Yellow Winterberry": (77064, ItemClassification.filler),
    "Guidebook": (77065, ItemClassification.useful),
    "Strange Gem": (77066, ItemClassification.filler),
    "Egg": (77067, ItemClassification.useful),
    "Turkey": (77068, ItemClassification.useful),
    #endregion
}