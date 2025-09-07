import requests
import re
from bs4 import BeautifulSoup

def get_all_item_ids(url):
    print(f"Fetching item IDs from {url}...")
    try:
        response = requests.get(url, timeout=10)
        response.raise_for_status()
    except requests.exceptions.RequestException as e:
        print(f"Error: Could not fetch the URL. {e}")
        return None

    soup = BeautifulSoup(response.content, 'html.parser')

    item_rows = soup.find_all('tr', class_=lambda c: c and 'memitem' in c)

    item_id_map = {}

    for row in item_rows:
        cells = row.find_all('td')
        if len(cells) > 1:
            data_cell = cells[1]
            name_tag = data_cell.find('b')

            if name_tag:
                item_name = name_tag.text.strip()
                try:
                    full_text = data_cell.get_text(separator=" ", strip=True)
                    item_id_str = full_text.split('=')[-1].strip()
                    item_id = int(item_id_str)
                    item_id_map[item_name.lower()] = (item_name, item_id)
                except (ValueError, IndexError):
                    continue

    print(f"Successfully parsed {len(item_id_map)} item IDs.")
    return item_id_map

def get_light_source_lists():
    exact_match_lights = [
        "Torch", "CursedTorch", "IchorTorch", "CoralTorch", "Candle", "Glowstick",
        "StickyGlowstick", "BouncyGlowstick", "FairyGlowstick", "SpelunkerGlowstick",
        "MiningHelmet", "JellyfishNecklace", "Magiluminescence", "FlareGun", "Flare",
        "BlueFlare", "ReleaseLantern", "Nightglow", "Fireplace", "TikiTorch", "LampPost",
        "BlueJellyfishJar", "PinkJellyfishJar", "GreenJellyfishJar",
        "BlueFairyJar", "PinkFairyJar", "GreenFairyJar",
        "HeartLantern", "StarinaBottle", "FireflyinaBottle", "LightningBuginaBottle",
        "LavaflyinaBottle", "SoulinaBottle", # Corrected from "SoulofNightinaBottle"
        "SkullLantern", "JackOLantern", "DiscoBall", "LavaLamp", "WireBulb",
        "ShadowOrb", "CrimsonHeart", "MagicLantern", "FairyBell", "CreeperEgg",
        "WispinaBottle", "SuspiciousLookingTentacle", "PumpkinScentedCandle",
        "JewelofLight", "ShinePotion", "AncientHorn", "ReindeerBells", "WitchsBroom",
        "ChlorophyteMask", "CrystalShard", "Campfire", "Furnace", "Hellforge",
        "AdamantiteForge", "TitaniumForge", "GlassKiln", "LihzahrdFurnace",
        "RocketBoots", "Jetpack", "VortexBooster", "ButterflyWings", "FairyWings",
        "FlameWings", "FrozenWings", "MothronWings", "DemoniteOre", "DemoniteBrick",
        "CrimtaneOre", "CrimtaneBrick", "Meteorite", "MeteoriteBrick", "ShroomitePlating",
        "LavafallBlock", "LivingFireBlock", "RainbowBrick", "MartianConduitPlating",
        "SolarFragmentBlock", "VortexFragmentBlock", "NebulaFragmentBlock", "StardustFragmentBlock",
        "EchoBlock", "Blinkroot", "Fireblossom", "Moonglow", "Shiverthorn", "JungleSpores",
        "Mushroom", "GlowingMushroom", "LavaMoss", "KryptonMoss", "XenonMoss", "ArgonMoss",
        "NeonMoss", "HeliumMoss", "StrangeGlowingMushroom", "BrokenHeart", "CursedSapling",
        "OrnateShadowKey", "LightningCarrot", "BallOfFuseWire", # Corrected casing
        "LavaBucket", "Teleporter", "FrostHelmet", "ForbiddenTreads", "GuardianGolem",
        "Yoraiz0rsSpell", "LazuresBarrierPlatform",
    ]

    generic_match_lights = [
        "Chandelier",
        "Candelabra",
        "Lantern",
        "Lamp",
        "GemsparkBlock",
        "ChristmasLights"
    ]
    
    return sorted(list(set(exact_match_lights))), sorted(list(set(generic_match_lights)))


if __name__ == "__main__":
    TMOD_URL = "https://docs.tmodloader.net/docs/stable/class_item_i_d.html"

    all_items = get_all_item_ids(TMOD_URL)

    if all_items:
        exact_light_names, generic_light_names = get_light_source_lists()

        found_lights = {}
        not_found_lights = []

        for name in exact_light_names:
            lower_name = name.lower()
            if lower_name in all_items:
                proper_name, item_id = all_items[lower_name]
                found_lights[proper_name] = item_id
            else:
                not_found_lights.append(name)

        print("\nSearching for generic item categories...")
        for generic_name in generic_light_names:
            matches = {}
            lower_generic = generic_name.lower()
            for key, (proper_name, item_id) in all_items.items():
                if lower_generic in key:
                    matches[proper_name] = item_id

            if matches:
                found_lights[f"Matches for '{generic_name}'"] = matches
            else:
                not_found_lights.append(generic_name)

        print("\n" + "="*50)
        print("MATCHED LIGHT SOURCE ITEMS AND THEIR IDs")
        print("="*50)

        sorted_keys = sorted(found_lights.keys())

        for key in sorted_keys:
            value = found_lights[key]
            if isinstance(value, dict): 
                print(f"\n--- {key} ---")
                for name, item_id in sorted(value.items()):
                    print(f"  - {name}: {item_id}")
            else:
                print(f"- {key}: {value}")

        if not_found_lights:
            print("\n" + "="*50)
            print(f"COULD NOT FIND {len(not_found_lights)} ITEMS")
            print("="*50)
            print("These may be from a newer game version or have a very different internal name:")
            for name in sorted(not_found_lights):
                print(f"- {name}")