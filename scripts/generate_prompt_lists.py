# CHANGE LOG
# - 2026-03-06 | Request: Tag-only mode | Target the library root for generated lists.
# - 2026-01-05 | Fix: Dresses count | Add missing entry for validation.
# - 2026-01-05 | Fix: ShotType count | Add missing entry for validation.

import os
from datetime import datetime

root = os.path.join(os.environ.get("LOCALAPPDATA", ""), "PromptLoom", "Library")
if not os.path.isdir(root):
    raise SystemExit(f"Library not found: {root}")

date = datetime.now().strftime("%Y-%m-%d")
header = f"# CHANGE LOG\n# - {date} | Request: Regenerate prompt lists | Initial content.\n"

data = {}
data.update({
    os.path.join("Composition", "Camera", "ShotType.txt"): [
        "close-up", "extreme close-up", "tight close-up", "headshot", "beauty shot", "portrait", "profile shot", "three quarter view",
        "front view", "back view", "side view", "full body shot", "half body shot", "upper body shot", "lower body shot", "waist shot",
        "hip shot", "knee shot", "bust shot", "shoulder shot", "medium shot", "medium close-up", "medium full shot", "full shot",
        "wide shot", "establishing shot", "environmental portrait", "studio shot", "lookbook shot", "editorial shot", "catalog shot",
        "lifestyle shot", "posed shot", "candid shot", "walking shot", "sitting shot", "standing shot", "reclining shot", "over the shoulder shot",
        "silhouette shot", "shadow shot", "reflection shot", "mirror shot", "window shot", "doorway shot", "framed shot", "centered shot",
        "off center shot", "rule of thirds shot", "macro shot",
    ],
    os.path.join("Composition", "Camera", "CameraAngle.txt"): [
        "eye level angle", "high angle", "low angle", "bird eye view", "worm eye view", "overhead angle", "top down angle", "bottom up angle",
        "three quarter angle", "profile angle", "front angle", "rear angle", "side angle", "dutch angle", "tilted angle", "canted angle",
        "over the shoulder angle", "diagonal angle", "straight on angle", "center angle", "left angle", "right angle", "front left angle",
        "front right angle", "rear left angle", "rear right angle", "left profile angle", "right profile angle", "top left angle",
        "top right angle", "low left angle", "low right angle", "high left angle", "high right angle", "overhead left angle", "overhead right angle",
        "under chin angle", "chin level angle", "brow level angle", "waist level angle", "hip level angle", "knee level angle", "ankle level angle",
        "ground level angle", "ceiling level angle", "pan angle", "tilt angle", "roll angle", "side tilt angle", "forward tilt angle", "back tilt angle",
    ],
    os.path.join("Composition", "Lens", "LensType.txt"): [
        "prime lens", "zoom lens", "macro lens", "telephoto lens", "wide angle lens", "ultra wide lens", "standard lens", "portrait lens",
        "tilt shift lens", "fish eye lens", "soft focus lens", "anamorphic lens", "pancake lens", "fast lens", "slow lens", "apochromatic lens",
        "telephoto zoom lens", "wide zoom lens", "normal zoom lens", "long zoom lens", "variable aperture lens", "constant aperture lens",
        "stabilized lens", "manual focus lens", "auto focus lens", "fixed focus lens", "retrofocus lens", "mirror lens", "catadioptric lens",
        "macro zoom lens", "close focus lens", "portrait prime lens", "wide prime lens", "short telephoto lens", "long telephoto lens",
        "ultra telephoto lens", "short macro lens", "long macro lens", "cinema lens", "cine prime", "cine zoom", "parfocal lens",
        "varifocal lens", "focus by wire lens", "vintage lens", "modern lens", "coated lens", "uncoated lens", "soft lens", "sharp lens",
    ],
    os.path.join("Composition", "Lens", "FocalLength.txt"): [
        "10mm lens", "12mm lens", "14mm lens", "16mm lens", "18mm lens", "20mm lens", "21mm lens", "24mm lens", "28mm lens", "30mm lens",
        "35mm lens", "40mm lens", "45mm lens", "50mm lens", "55mm lens", "58mm lens", "60mm lens", "65mm lens", "70mm lens", "75mm lens",
        "80mm lens", "85mm lens", "90mm lens", "95mm lens", "100mm lens", "105mm lens", "110mm lens", "120mm lens", "135mm lens", "150mm lens",
        "160mm lens", "170mm lens", "180mm lens", "200mm lens", "210mm lens", "220mm lens", "240mm lens", "250mm lens", "300mm lens", "400mm lens",
        "500mm lens", "600mm lens", "70-200mm lens", "24-70mm lens", "16-35mm lens", "18-55mm lens", "28-75mm lens", "35-105mm lens", "50-150mm lens", "100-400mm lens",
    ],
    os.path.join("Composition", "Settings", "Backdrop.txt"): [
        "seamless backdrop", "paper backdrop", "muslin backdrop", "fabric backdrop", "vinyl backdrop", "cyclorama", "studio wall", "plaster wall",
        "brick wall", "concrete wall", "stone wall", "tile wall", "wood panel wall", "shoji screen", "curtain", "sheer curtain", "blackout curtain",
        "beaded curtain", "venetian blinds", "frosted glass", "window wall", "mirror wall", "metal wall", "grid wall", "chain link fence",
        "panel screen", "room divider", "rolling backdrop", "lightbox backdrop", "scrim", "canvas drop", "textured drop", "linen drop",
        "paper roll", "fabric roll", "backdrop stand", "studio corner", "arched backdrop", "cove wall", "set wall", "stage wall",
        "floor sweep", "paper sweep", "fabric sweep", "wood floor", "concrete floor", "tile floor", "laminate floor", "marble floor",
        "backdrop frame", "backdrop rig",
    ],
})
data.update({
    os.path.join("Person", "Meta", "AdultDescriptor.txt"): [
        "adult woman", "adult female", "grown woman", "mature woman", "woman", "female", "female adult", "adult model", "adult beauty", "adult glamour",
        "adult portrait subject", "adult muse", "adult fashion model", "adult lingerie model", "adult swimwear model", "adult fitness model", "adult art model",
        "adult figure model", "adult life model", "adult studio model", "adult photo model", "adult portrait model", "adult editorial model", "adult runway model",
        "adult print model", "adult commercial model", "adult catalog model", "adult pinup model", "adult boudoir model", "adult beauty model", "adult face model",
        "adult hair model", "adult hand model", "adult foot model", "adult body model", "adult glamour model", "adult lifestyle model", "adult lookbook model",
        "adult campaign model", "adult brand model", "adult runway talent", "adult fashion talent", "adult beauty talent", "adult female subject",
        "adult woman subject", "adult female portrait", "adult woman portrait", "adult woman model", "adult female muse", "adult female talent",
    ],
    os.path.join("Person", "Meta", "Ethnicity.txt"): [
        "African", "African American", "Afro Caribbean", "North African", "East African", "West African", "Central African", "South African", "East Asian",
        "Southeast Asian", "South Asian", "Central Asian", "Middle Eastern", "Arab", "Persian", "Turkish", "Mediterranean", "Latina", "Hispanic",
        "Indigenous", "Native American", "Pacific Islander", "Polynesian", "Micronesian", "Melanesian", "European", "Northern European", "Southern European",
        "Eastern European", "Western European", "Slavic", "Celtic", "Nordic", "Scandinavian", "Balkan", "Greek", "Italian", "Spanish", "Portuguese", "French",
        "German", "British", "Irish", "Scottish", "Welsh", "Jewish", "Berber", "Korean", "Japanese", "Chinese", "Vietnamese",
    ],
    os.path.join("Person", "Meta", "Nationality.txt"): [
        "American", "Canadian", "Mexican", "Brazilian", "Argentinian", "Chilean", "Peruvian", "Colombian", "Venezuelan", "British", "Irish", "Scottish",
        "Welsh", "French", "German", "Italian", "Spanish", "Portuguese", "Dutch", "Belgian", "Swiss", "Austrian", "Swedish", "Norwegian", "Danish", "Finnish",
        "Icelandic", "Polish", "Czech", "Slovak", "Hungarian", "Romanian", "Bulgarian", "Greek", "Turkish", "Russian", "Ukrainian", "Georgian", "Armenian",
        "Israeli", "Egyptian", "Moroccan", "South African", "Nigerian", "Kenyan", "Ethiopian", "Indian", "Pakistani", "Bangladeshi", "Chinese", "Japanese", "Korean", "Vietnamese",
    ],
    os.path.join("Person", "Meta", "Occupation.txt"): [
        "actor", "singer", "dancer", "ballet dancer", "model", "photographer", "artist", "painter", "sculptor", "designer", "fashion designer", "makeup artist",
        "hair stylist", "chef", "barista", "bartender", "waitress", "server", "nurse", "doctor", "surgeon", "dentist", "lawyer", "judge", "teacher", "professor",
        "writer", "journalist", "editor", "musician", "guitarist", "pianist", "violinist", "athlete", "runner", "swimmer", "gymnast", "yoga instructor", "trainer",
        "pilot", "flight attendant", "engineer", "architect", "scientist", "researcher", "librarian", "therapist", "psychologist", "businesswoman", "entrepreneur", "manager", "producer", "director",
    ],
    os.path.join("Person", "Meta", "ModelType.txt"): [
        "fashion model", "runway model", "editorial model", "commercial model", "beauty model", "glamour model", "pinup model", "lingerie model", "swimwear model",
        "fitness model", "art model", "life model", "figure model", "hand model", "foot model", "hair model", "makeup model", "catalog model", "print model",
        "promo model", "hostess model", "calendar model", "bridal model", "couture model", "lookbook model", "ecommerce model", "social model", "influencer model",
        "campaign model", "brand model", "lifestyle model", "studio model", "portrait model", "face model", "body model", "parts model", "catalogue model",
        "trade show model", "showroom model", "press model", "advertising model", "agency model", "test shoot model", "commercial face", "beauty face", "glamour face",
        "editorial face", "lookbook face", "portfolio model", "agency talent",
    ],
})
data.update({
    os.path.join("Head", "Face", "Eyes.txt"): [
        "almond eyes", "round eyes", "hooded eyes", "monolid eyes", "deep set eyes", "wide set eyes", "close set eyes", "upturned eyes", "downturned eyes",
        "large eyes", "small eyes", "bright eyes", "soft eyes", "sharp eyes", "cat eyes", "doe eyes", "bedroom eyes", "half lidded eyes", "wide open eyes",
        "sparkling eyes", "glossy eyes", "smoky eyes", "intense eyes", "gentle eyes", "steady gaze", "direct gaze", "side glance", "downcast eyes", "upward gaze",
        "dreamy eyes", "bold eyes", "calm eyes", "focused eyes", "relaxed eyes", "alert eyes", "serene eyes", "piercing eyes", "warm eyes", "cool eyes",
        "arched brows", "straight brows", "soft brows", "bold brows", "thick brows", "thin brows", "natural brows", "groomed brows", "clean brows", "full lashes",
        "long lashes", "short lashes", "curled lashes", "thick lashes",
    ],
    os.path.join("Head", "Face", "Lips.txt"): [
        "full lips", "thin lips", "soft lips", "plump lips", "heart shaped lips", "bow lips", "wide lips", "narrow lips", "defined lips", "smooth lips",
        "glossy lips", "matte lips", "natural lips", "bitten lips", "pout", "lip pout", "lip press", "lip bite", "lip gap", "closed lips",
        "open lips", "parted lips", "smile", "soft smile", "wide smile", "subtle smile", "smirk", "half smile", "grin", "neutral mouth",
        "lip contour", "lip line", "lip curve", "lip shape", "lip volume", "lip peak", "lip cupid bow", "upper lip", "lower lip", "lip edge",
        "lip center", "lip highlight", "lip shadow", "lip texture", "lip detail", "lip profile", "lip silhouette", "lip focus", "lip front view", "lip side view",
    ],
    os.path.join("Head", "Hair", "HairLength.txt"): [
        "buzz cut", "crew cut", "pixie cut", "short crop", "ear length hair", "cheek length hair", "chin length hair", "jaw length hair", "bob cut",
        "lob cut", "shoulder length hair", "collarbone length hair", "chest length hair", "bust length hair", "mid back hair", "waist length hair",
        "hip length hair", "tailbone length hair", "floor length hair", "long hair", "short hair", "medium hair", "micro bangs", "baby bangs",
        "brow length bangs", "eye length bangs", "cheek length layers", "chin length layers", "shoulder layers", "long layers", "face framing layers",
        "blunt cut", "layered cut", "shag cut", "wolf cut", "pageboy cut", "midi cut", "midi bob", "long bob", "mini bob", "shoulder skim hair",
        "collarbone bob", "chest skim hair", "waist skim hair", "hip skim hair", "tailbone hair", "floor skim hair", "neck length hair", "nape length hair", "jawline bob", "cheekbone bob", "lip length hair",
    ],
    os.path.join("Head", "Hair", "HairStyle.txt"): [
        "ponytail", "high ponytail", "low ponytail", "side ponytail", "bubble ponytail", "braid", "french braid", "dutch braid", "fishtail braid", "rope braid",
        "crown braid", "halo braid", "side braid", "double braid", "box braids", "micro braids", "twists", "flat twists", "cornrows", "bun",
        "high bun", "low bun", "messy bun", "sleek bun", "top knot", "chignon", "beehive", "bouffant", "updo", "half updo",
        "half up half down", "side swept hair", "slicked back hair", "wet look hair", "spiky hair", "wavy hair", "curly hair", "coily hair", "straight hair",
        "beach waves", "finger waves", "victory rolls", "pin curls", "roller curls", "blowout", "feathered hair", "layered waves", "voluminous hair", "side part", "middle part",
    ],
    os.path.join("Head", "Makeup", "MakeupStyle.txt"): [
        "natural makeup", "no makeup look", "soft glam", "full glam", "smoky eye", "cat eye", "winged eyeliner", "graphic liner", "tightline", "bold liner",
        "soft liner", "lower liner", "upper liner", "waterline liner", "ombre lips", "glossy lips", "matte lips", "satin lips", "lip stain", "lip tint",
        "overlined lips", "defined lips", "blurred lips", "soft blush", "cream blush", "powder blush", "contour", "bronzer", "highlight", "strobe",
        "dewy skin", "matte skin", "satin skin", "soft focus makeup", "airbrush makeup", "luminous skin", "glass skin", "clean skin", "soft shadow", "shimmer shadow",
        "matte shadow", "foil shadow", "cut crease", "halo eye", "inner corner highlight", "tight highlight", "freckle makeup", "beauty marks", "soft brows", "bold brows",
    ],
})
data.update({
    os.path.join("Body", "Ass and Pussy", "AssAndPussy.txt"): [
        "ass", "butt", "booty", "backside", "rear", "buttocks", "glutes", "gluteal", "cheeks", "cheek spread", "ass crack", "butt cleft", "cleft", "glute crease",
        "hip curve", "hip dip", "hip line", "hip bone", "pelvis", "pelvic bone", "pelvic curve", "pussy", "vulva", "labia", "labia majora", "labia minora",
        "clit", "clitoris", "clitoral hood", "mons pubis", "vaginal opening", "pussy lips", "inner lips", "outer lips", "pussy crease", "crotch",
        "groin", "pubic mound", "thong line", "cheek line", "butt curve", "butt shape", "butt profile", "butt silhouette", "ass contour", "ass line",
        "ass curve", "ass profile", "ass silhouette", "butt highlight", "butt shadow",
    ],
    os.path.join("Body", "Chest", "Chest.txt"): [
        "chest", "upper chest", "lower chest", "sternum", "breastbone", "ribcage", "ribs", "collarbone", "clavicle", "cleavage", "decolletage", "bust",
        "bosom", "breasts", "breast curve", "breast contour", "breast line", "breast silhouette", "breast profile", "bustline", "bust curve", "bust contour",
        "bust profile", "bust silhouette", "chest curve", "chest contour", "chest line", "chest profile", "chest silhouette", "chest shape", "chest center",
        "chest hollow", "chest highlight", "chest shadow", "breast base", "breast fold", "underbust", "upper bust", "inner bust", "outer bust", "bust gap",
        "breast separation", "breast spacing", "chest spacing", "chest symmetry", "chest lift", "chest movement", "bust movement", "bust sway", "bust bounce", "bust volume",
    ],
    os.path.join("Body", "Hands and Arms", "Hands.txt"): [
        "hands", "fingers", "fingertips", "finger pads", "finger joints", "finger knuckles", "finger nails", "nails", "nail beds", "cuticles", "palms", "palm lines",
        "palm crease", "thumb", "index finger", "middle finger", "ring finger", "little finger", "wrist", "wrist bones", "wrist curve", "wrist line",
        "forearm", "forearm line", "forearm curve", "elbow", "elbow crease", "upper arm", "bicep", "tricep", "arm curve", "arm line", "arm contour",
        "arm silhouette", "hand pose", "hand gesture", "hand placement", "hand on hip", "hand in hair", "hand on thigh", "hand on waist", "hand on chest",
        "hand on neck", "hand on shoulder", "hand clasp", "hand grip", "arm bend", "arm extension", "arm cross", "arm lift", "arm raise", "arm reach",
    ],
    os.path.join("Body", "Legs and Feet", "Legs.txt"): [
        "legs", "thighs", "thigh gap", "inner thigh", "outer thigh", "upper thigh", "lower thigh", "hip to thigh", "quad", "quads", "hamstring", "calf",
        "calves", "shin", "shin line", "knee", "knees", "knee cap", "knee crease", "leg line", "leg curve", "leg contour", "leg profile", "leg silhouette",
        "leg length", "leg shape", "leg stance", "leg cross", "leg extension", "leg bend", "leg lift", "leg raise", "leg press", "leg stretch",
        "long legs", "short legs", "slim legs", "toned legs", "curvy legs", "muscular legs", "smooth legs", "leg focus", "leg detail", "leg highlight",
        "leg shadow", "leg angle", "leg pose", "standing legs", "sitting legs", "crossed legs",
    ],
    os.path.join("Body", "Torso", "Torso.txt"): [
        "torso", "waist", "hips", "hip line", "hip curve", "hip contour", "abdomen", "stomach", "belly", "navel", "navel ring", "abs", "abdominals", "waistline",
        "lower waist", "upper waist", "midriff", "side waist", "love handles", "obliques", "ribcage", "ribs", "back", "lower back", "upper back", "back curve",
        "back line", "back contour", "back profile", "back silhouette", "spine", "shoulders", "shoulder line", "shoulder curve", "shoulder contour", "shoulder blade",
        "collarbone", "clavicle", "neck", "nape", "torso line", "torso curve", "torso contour", "torso profile", "torso silhouette", "torso shape", "torso length",
        "body core", "body line", "body contour", "body profile", "body silhouette",
    ],
})
data.update({
    os.path.join("Clothing", "90s", "Tops.txt"): [
        "crop top", "baby tee", "tank top", "camisole", "spaghetti strap top", "tube top", "halter top", "mesh top", "sheer top", "button up shirt",
        "flannel shirt", "denim jacket", "bomber jacket", "windbreaker", "hoodie", "sweatshirt", "graphic tee", "ribbed tee", "henley shirt", "turtleneck",
        "mock neck top", "cardigan", "knit sweater", "pullover", "zip hoodie", "track jacket", "leather jacket", "fitted blazer", "oversized blazer", "vest",
        "corset top", "bustier", "lace top", "satin top", "velvet top", "sequin top", "halter camisole", "wrap top", "tie front top", "cowl neck top",
        "off shoulder top", "one shoulder top", "long sleeve tee", "short sleeve tee", "raglan tee", "baseball tee", "crewneck tee", "v neck tee", "scoop neck tee", "boat neck top",
    ],
    os.path.join("Clothing", "90s", "Bottoms.txt"): [
        "baggy jeans", "mom jeans", "straight leg jeans", "bootcut jeans", "flare jeans", "wide leg jeans", "low rise jeans", "high rise jeans", "cargo pants",
        "track pants", "wind pants", "joggers", "leggings", "bike shorts", "shorts", "denim shorts", "pleated skirt", "mini skirt", "midi skirt",
        "maxi skirt", "slip skirt", "denim skirt", "cargo skirt", "pencil skirt", "a line skirt", "overalls", "overall shorts", "capri pants", "cropped pants",
        "boyfriend jeans", "skinny jeans", "raw hem jeans", "distressed jeans", "corduroy pants", "corduroy skirt", "satin pants", "leather pants", "vinyl pants",
        "pleated pants", "paperbag waist pants", "drawstring pants", "track shorts", "gym shorts", "tennis skirt", "wrap skirt", "skort", "culottes", "flare pants",
        "kick flare pants", "parachute pants",
    ],
    os.path.join("Clothing", "90s", "Dresses.txt"): [
        "slip dress", "mini dress", "midi dress", "maxi dress", "bodycon dress", "tank dress", "t shirt dress", "sweater dress", "halter dress", "strapless dress",
        "spaghetti strap dress", "cami dress", "wrap dress", "fit and flare dress", "a line dress", "sheath dress", "shift dress", "babydoll dress", "empire waist dress",
        "peasant dress", "denim dress", "corduroy dress", "velvet dress", "satin dress", "lace dress", "mesh dress", "sequin dress", "glitter dress", "floral dress",
        "polka dot dress", "plaid dress", "striped dress", "ruched dress", "slit dress", "cowl neck dress", "off shoulder dress", "one shoulder dress", "backless dress",
        "halter mini dress", "halter maxi dress", "slip maxi dress", "slip mini dress", "shirt dress", "button front dress", "pinafore dress", "overall dress", "tank midi dress", "bodycon mini dress", "bodycon midi dress", "halter midi dress",
    ],
    os.path.join("Clothing", "Historical", "Eras.txt"): [
        "ancient era", "classical era", "medieval era", "renaissance era", "baroque era", "rococo era", "georgian era", "regency era", "victorian era", "edwardian era",
        "gilded age", "belle epoque", "art nouveau era", "art deco era", "jazz age", "roaring twenties", "war era", "postwar era", "mid century era", "tudor era",
        "elizabethan era", "jacobean era", "carolingian era", "byzantine era", "roman era", "greek era", "egyptian era", "mesopotamian era", "prehistoric era",
        "stone age", "bronze age", "iron age", "viking age", "sengoku era", "heian era", "mughal era", "ottoman era", "persian era", "celtic era",
        "gaelic era", "colonial era", "frontier era", "civil era", "industrial era", "victorian gothic", "edwardian belle", "regency romance", "baroque court", "rococo court", "renaissance court",
    ],
    os.path.join("Clothing", "Historical", "Garments.txt"): [
        "corset", "stays", "chemise", "petticoat", "crinoline", "bustle", "tunic", "toga", "chiton", "stola", "peplos", "robe", "gown", "cloak", "cape", "doublet",
        "jerkin", "hose", "breeches", "pantaloons", "shift", "smock", "waistcoat", "bodice", "kirtle", "surcoat", "tabard", "farthingale", "ruff", "corsage", "girdle",
        "sash", "sari", "kimono", "hanbok", "qipao", "cheongsam", "kaftan", "abaya", "caftan", "haori", "hakama", "sarong", "wrap skirt", "shawl", "mantle", "veil", "headdress", "bonnet", "corset belt", "lace collar",
    ],
})
data.update({
    os.path.join("Footwear", "Heels", "Heels.txt"): [
        "stiletto heels", "kitten heels", "block heels", "chunky heels", "platform heels", "wedge heels", "pumps", "court shoes", "peep toe heels", "open toe heels",
        "closed toe heels", "ankle strap heels", "T strap heels", "slingback heels", "dorsay heels", "mary jane heels", "cage heels", "strappy heels", "lace up heels", "mule heels",
        "slide heels", "espadrille heels", "cone heels", "spool heels", "flared heels", "sculpted heels", "stacked heels", "high heel sandals", "evening heels", "dance heels",
        "bridal heels", "office heels", "patent heels", "suede heels", "leather heels", "mesh heels", "satin heels", "velvet heels", "croc heels", "pointed toe heels",
        "round toe heels", "square toe heels", "clear heel sandals", "perspex heels", "cap toe heels", "cutout heels", "two piece heels", "wraparound heels", "architectural heels", "lucite heels",
    ],
    os.path.join("Footwear", "Boots", "Boots.txt"): [
        "ankle boots", "chelsea boots", "combat boots", "knee high boots", "over the knee boots", "thigh high boots", "platform boots", "stiletto boots", "block heel boots", "wedge boots",
        "lace up boots", "zipper boots", "buckled boots", "riding boots", "western boots", "cowgirl boots", "motorcycle boots", "harness boots", "work boots", "hiking boots",
        "snow boots", "rain boots", "rubber boots", "leather boots", "suede boots", "patent boots", "chunky boots", "slouch boots", "sock boots", "cutout boots",
        "peep toe boots", "open toe boots", "side zip boots", "front zip boots", "back zip boots", "pull on boots", "mid calf boots", "lug sole boots", "crepe sole boots", "platform ankle boots",
        "kitten heel boots", "stacked heel boots", "knee high riding boots", "over the knee stretch boots", "thigh high lace up boots", "combat ankle boots", "western ankle boots", "work ankle boots", "rain ankle boots", "alpine boots",
    ],
    os.path.join("Footwear", "Sandals", "Sandals.txt"): [
        "flat sandals", "slide sandals", "thong sandals", "strappy sandals", "gladiator sandals", "ankle strap sandals", "toe ring sandals", "slingback sandals", "mule sandals", "espadrille sandals",
        "platform sandals", "wedge sandals", "block heel sandals", "kitten heel sandals", "stiletto sandals", "sport sandals", "hiking sandals", "fisherman sandals", "jelly sandals", "cork sandals",
        "braided sandals", "lace up sandals", "crisscross sandals", "T strap sandals", "cage sandals", "cutout sandals", "peep toe sandals", "open toe sandals", "buckle sandals", "wraparound sandals",
        "crossover sandals", "two strap sandals", "three strap sandals", "multi strap sandals", "slip on sandals", "beach sandals", "pool sandals", "flip flops", "foam sandals", "rubber sandals",
        "leather sandals", "suede sandals", "satin sandals", "canvas sandals", "mesh sandals", "knit sandals", "ankle tie sandals", "platform slides", "heeled slides", "minimalist sandals",
    ],
    os.path.join("Footwear", "Sneakers", "Sneakers.txt"): [
        "low top sneakers", "high top sneakers", "mid top sneakers", "slip on sneakers", "lace up sneakers", "canvas sneakers", "leather sneakers", "suede sneakers", "knit sneakers", "mesh sneakers",
        "running shoes", "training shoes", "cross trainers", "basketball shoes", "tennis shoes", "walking shoes", "skate shoes", "vulcanized sneakers", "cupsole sneakers", "platform sneakers",
        "chunky sneakers", "retro sneakers", "minimalist sneakers", "trail runners", "track shoes", "court sneakers", "lifestyle sneakers", "fashion sneakers", "sport sneakers", "gym sneakers",
        "court trainers", "street sneakers", "casual sneakers", "low profile sneakers", "high profile sneakers", "sock sneakers", "insulated sneakers", "waterproof sneakers", "laceless sneakers", "elastic lace sneakers",
        "knit upper sneakers", "foam sole sneakers", "air cushioned sneakers", "gel cushioned sneakers", "crossfit shoes", "dance sneakers", "skate high tops", "hiking sneakers", "court runners", "everyday sneakers",
    ],
    os.path.join("Footwear", "Flats", "Flats.txt"): [
        "ballet flats", "pointed toe flats", "round toe flats", "square toe flats", "dorsay flats", "mary jane flats", "loafers", "penny loafers", "tassel loafers", "driving moccasins",
        "moccasins", "boat shoes", "oxfords", "derby shoes", "brogues", "lace up flats", "slip on flats", "espadrille flats", "flat mules", "flat slides",
        "slingback flats", "ankle strap flats", "T strap flats", "peep toe flats", "open toe flats", "cap toe flats", "cutout flats", "woven flats", "braided flats", "knit flats",
        "mesh flats", "leather flats", "suede flats", "canvas flats", "rubber flats", "platform flats", "flatforms", "skimmer flats", "ballerina shoes", "smoking slippers",
        "house slippers", "scuff slippers", "mule slippers", "espadrille shoes", "fisherman flats", "gladiator flats", "thong flats", "toe ring flats", "ankle wrap flats", "foldable flats",
    ],
})
data.update({
    os.path.join("Feet", "Toes", "Toes.txt"): [
        "toes", "big toe", "second toe", "middle toe", "fourth toe", "little toe", "toe tips", "toe pads", "toe knuckles", "toe joints",
        "toe spread", "toe splay", "toe curl", "toe point", "toe flex", "toe wiggle", "toe lift", "toe press", "toe grip", "toe extension",
        "toe nails", "toenails", "toenail beds", "toe cuticles", "toe ridges", "toe rings", "toe separators", "toe alignment", "toe arch", "toe crease",
        "toe webbing", "toe fan", "toe line", "toe silhouette", "toe profile", "toe shape", "toe length", "toe spacing", "toe taper", "toe mound",
        "toe bridge", "toe base", "toe tip curve", "toe tip line", "toe print", "toe focus", "toe pose", "toe point pose", "toe curl pose", "toe press pose",
    ],
    os.path.join("Feet", "Soles", "Soles.txt"): [
        "soles", "foot soles", "sole curve", "sole line", "sole arch", "heel pad", "ball of foot", "midfoot", "forefoot", "instep",
        "footbed", "foot print", "sole print", "sole texture", "sole contour", "sole crease", "sole flex", "sole stretch", "sole press", "sole contact",
        "sole lift", "sole arch line", "sole ridge", "sole hollow", "sole edge", "sole outline", "sole profile", "sole shape", "sole surface", "sole plane",
        "sole angle", "sole shadow", "sole highlight", "sole focus", "sole detail", "heel cup", "heel edge", "heel curve", "heel line", "heel silhouette",
        "ball line", "ball curve", "ball ridge", "ball press", "instep line", "instep curve", "midfoot line", "midfoot curve", "sole center", "sole tip",
    ],
    os.path.join("Feet", "Ankles", "Ankles.txt"): [
        "ankles", "ankle bone", "ankle joint", "ankle curve", "ankle line", "ankle hollow", "ankle point", "ankle ridge", "ankle silhouette", "ankle profile",
        "ankle contour", "ankle taper", "ankle width", "ankle height", "ankle flex", "ankle bend", "ankle extension", "ankle rotation", "ankle stance", "ankle alignment",
        "ankle angle", "ankle crease", "ankle dimple", "ankle highlight", "ankle shadow", "ankle focus", "ankle detail", "inner ankle", "outer ankle", "ankle top",
        "ankle base", "ankle bridge", "ankle slope", "ankle point line", "ankle point curve", "ankle socket", "ankle line view", "ankle side view", "ankle front view", "ankle back view",
        "ankle support", "ankle tension", "ankle stretch", "ankle press", "ankle lift", "ankle lock", "ankle sway", "ankle turnout", "ankle point pose", "ankle roll",
    ],
    os.path.join("Feet", "Arches", "Arches.txt"): [
        "arches", "foot arch", "high arch", "low arch", "medium arch", "arch curve", "arch line", "arch ridge", "arch hollow", "arch crest",
        "arch peak", "arch slope", "arch angle", "arch height", "arch length", "arch shape", "arch profile", "arch silhouette", "arch detail", "arch focus",
        "arch contour", "arch flex", "arch stretch", "arch tension", "arch press", "arch lift", "arch bend", "arch support", "arch spread", "arch narrow",
        "arch wide", "arch center", "arch base", "arch point", "arch rise", "arch dip", "arch spring", "arch view", "instep arch", "midfoot arch",
        "heel arch", "arch line view", "arch side view", "arch front view", "arch back view", "arch shadow", "arch highlight", "arch crease", "arch ridge line", "arch curve line",
    ],
    os.path.join("Feet", "Pedicure", "Pedicure.txt"): [
        "pedicure", "toenail polish", "toenail gloss", "toenail shine", "toenail finish", "toenail art", "toenail decals", "toenail gems", "toenail studs", "toenail stripes",
        "toenail dots", "toenail lines", "toenail tips", "french tips", "toe nail shape", "square nails", "squoval nails", "round nails", "oval nails", "almond nails",
        "coffin nails", "stiletto nails", "short nails", "medium nails", "long nails", "toe cuticle care", "cuticle oil", "cuticle push", "nail buffer", "nail file",
        "nail clip", "nail trim", "nail care", "nail scrub", "nail soak", "foot scrub", "foot soak", "foot lotion", "foot oil", "foot balm",
        "heel cream", "callus file", "callus stone", "pumice stone", "foot mask", "toe separator", "toe spacer", "toe splint", "nail strengthener", "nail top coat",
    ],
})
data.update({
    os.path.join("Boobs", "Breasts", "Breasts.txt"): [
        "breasts", "bust", "bosom", "chest", "mammaries", "breast tissue", "breast curve", "breast contour", "breast shape", "breast line",
        "breast profile", "breast silhouette", "breast fullness", "breast volume", "breast form", "breast mound", "breast peak", "breast slope", "breast crease", "breast fold",
        "breast base", "breast root", "breast projection", "breast roundness", "breast symmetry", "breast spacing", "breast separation", "breast lift", "breast hang", "breast weight",
        "breast bounce", "breast sway", "breast jiggle", "breast movement", "bustline", "bust curve", "bust contour", "bust profile", "bust silhouette", "bust shape",
        "bust fullness", "bust volume", "bust form", "bust mound", "chest curve", "chest contour", "chest line", "chest profile", "chest silhouette", "chest shape",
    ],
    os.path.join("Boobs", "Cleavage", "Cleavage.txt"): [
        "cleavage", "decolletage", "bustline", "cleavage line", "cleavage curve", "cleavage contour", "cleavage shadow", "cleavage highlight", "cleavage depth", "cleavage gap",
        "cleavage valley", "cleavage channel", "cleavage center", "cleavage seam", "cleavage ridge", "cleavage edge", "cleavage slope", "cleavage line view", "cleavage profile", "cleavage silhouette",
        "bust separation", "bust gap", "bust valley", "breast separation", "breast gap", "breast valley", "chest valley", "chest gap", "chest line", "neckline",
        "neckline dip", "neckline curve", "neckline line", "decolletage line", "decolletage curve", "decolletage contour", "decolletage shadow", "decolletage highlight", "upper chest", "upper bust",
        "upper cleavage", "center bust", "center chest", "inner bust", "inner chest", "cleavage focus", "cleavage detail", "cleavage depth line", "cleavage crease", "cleavage fold",
    ],
    os.path.join("Boobs", "Nipples", "Nipples.txt"): [
        "nipples", "nipple tips", "nipple buds", "nipple points", "nipple peaks", "nipple heads", "nipple base", "nipple areola", "nipple ring", "nipple edge",
        "nipple center", "nipple texture", "nipple detail", "nipple profile", "nipple silhouette", "nipple contour", "nipple shadow", "nipple highlight", "nipple ridge", "nipple crease",
        "nipple line", "nipple shape", "nipple size", "nipple height", "nipple angle", "nipple tilt", "nipple projection", "nipple mound", "nipple bump", "nipple poke",
        "nipple pinch", "nipple press", "nipple lift", "nipple drop", "nipple curve", "nipple focus", "nipple view", "nipple front view", "nipple side view", "nipple down view",
        "nipple closeup", "nipple ring view", "nipple piercings", "nipple barbell", "nipple stud", "nipple shield", "nipple jewelry", "nipple adornment", "nipple sensitivity", "nipple hardness",
    ],
    os.path.join("Boobs", "Areola", "Areola.txt"): [
        "areola", "areola ring", "areola edge", "areola border", "areola circle", "areola disk", "areola tint", "areola tone", "areola texture", "areola detail",
        "areola profile", "areola silhouette", "areola contour", "areola shadow", "areola highlight", "areola size", "areola shape", "areola diameter", "areola radius", "areola center",
        "areola line", "areola crease", "areola ridge", "areola bump", "areola swell", "areola stretch", "areola press", "areola lift", "areola drop", "areola curve",
        "areola focus", "areola view", "areola front view", "areola side view", "areola top view", "areola closeup", "areola surface", "areola texture line", "areola grain", "areola pores",
        "areola dots", "areola bumps", "areola ring line", "areola ring curve", "areola ring edge", "areola ring shape", "areola ring size", "areola ring view", "areola ring detail", "areola ring focus",
    ],
    os.path.join("Boobs", "Underbust", "Underbust.txt"): [
        "underbust", "underbust line", "underbust curve", "underbust contour", "underbust shadow", "underbust highlight", "underbust fold", "breast fold", "inframammary fold", "underbust crease",
        "underbust ridge", "underbust edge", "underbust slope", "underbust base", "underbust root", "underbust profile", "underbust silhouette", "underbust detail", "underbust focus", "underbust angle",
        "underbust depth", "underbust gap", "underbust valley", "underbust line view", "underbust front view", "underbust side view", "underbust top view", "underbust closeup", "underbust contour line", "underbust curve line",
        "underbust crease line", "underbust fold line", "breast base", "breast root line", "breast lower curve", "lower bust", "lower breast", "lower chest", "lower bustline", "lower chest line",
        "underbust seam", "underbust ridge line", "underbust detail line", "underbust highlight line", "underbust shadow line", "underbust skin", "underbust texture", "underbust shape", "underbust span", "underbust width",
    ],
})
data.update({
    os.path.join("Cosplay", "Outfits", "Outfits.txt"): [
        "maid outfit", "nurse outfit", "doctor outfit", "police outfit", "detective outfit", "secretary outfit", "office outfit", "business suit", "cocktail dress", "evening gown",
        "bodysuit", "catsuit", "latex catsuit", "leather catsuit", "jumpsuit", "flight attendant outfit", "pilot outfit", "bartender outfit", "waitress outfit", "chef outfit",
        "mechanic outfit", "artist outfit", "photographer outfit", "librarian outfit", "scientist outfit", "lab coat outfit", "firefighter outfit", "lifeguard outfit", "racer outfit", "biker outfit",
        "cowgirl outfit", "pirate outfit", "sailor outfit", "space suit", "astronaut suit", "superhero suit", "spy suit", "agent outfit", "fantasy gown", "princess gown",
        "queen gown", "goddess outfit", "fairy outfit", "angel outfit", "devil outfit", "witch outfit", "vampire outfit", "mermaid outfit", "mage outfit", "sorceress outfit",
    ],
    os.path.join("Cosplay", "Uniforms", "Uniforms.txt"): [
        "police uniform", "military uniform", "navy uniform", "air force uniform", "army uniform", "coast guard uniform", "flight attendant uniform", "pilot uniform", "nurse uniform", "doctor uniform",
        "surgeon scrubs", "hospital scrubs", "paramedic uniform", "firefighter uniform", "lifeguard uniform", "chef uniform", "waiter uniform", "waitress uniform", "bartender uniform", "barista uniform",
        "hotel uniform", "concierge uniform", "maid uniform", "housekeeper uniform", "security uniform", "prison guard uniform", "detective uniform", "postal uniform", "courier uniform", "delivery uniform",
        "mechanic uniform", "factory uniform", "lab uniform", "scientist uniform", "teacher uniform", "librarian uniform", "office uniform", "corporate uniform", "airline uniform", "train conductor uniform",
        "bus driver uniform", "taxi driver uniform", "racing uniform", "biker uniform", "sailor uniform", "cruise uniform", "spa uniform", "yoga instructor uniform", "dance uniform", "stage uniform",
    ],
    os.path.join("Cosplay", "Characters", "Characters.txt"): [
        "witch", "sorceress", "mage", "enchantress", "goddess", "queen", "princess", "priestess", "oracle", "seer",
        "angel", "devil", "demon", "vampire", "fairy", "elf", "mermaid", "siren", "nymph", "dryad",
        "astronaut", "space explorer", "space captain", "star pilot", "galactic officer", "time traveler", "android", "cyborg", "robotic woman", "alien woman",
        "pirate captain", "cowgirl", "sailor", "spy", "agent", "detective", "archaeologist", "adventurer", "explorer", "librarian",
        "artist", "musician", "dancer", "ballerina", "chef", "bartender", "photographer", "model", "actress", "singer",
    ],
    os.path.join("Cosplay", "Accessories", "Accessories.txt"): [
        "wig", "cape", "cloak", "mask", "eye mask", "tiara", "crown", "headpiece", "horns", "halo",
        "wings", "tail", "cat ears", "bunny ears", "headband", "choker", "collar", "arm cuffs", "arm bands", "gauntlets",
        "leg bands", "garter", "belt", "utility belt", "pouch", "satchel", "sash", "shoulder straps", "back harness", "body harness",
        "goggles", "visor", "face paint", "body paint", "tattoo decals", "stickers", "patches", "badges", "emblems", "brooch",
        "bracelet stack", "ring stack", "anklet", "glovelets", "fingerless gloves", "thigh highs", "stockings", "fishnet stockings", "leggings", "arm sleeves",
    ],
    os.path.join("Cosplay", "Themes", "Themes.txt"): [
        "steampunk", "cyberpunk", "sci fi", "fantasy", "dark fantasy", "urban fantasy", "high fantasy", "space opera", "retro futurism", "retro",
        "vintage", "noir", "gothic", "victorian", "edwardian", "baroque", "rococo", "art deco", "art nouveau", "modern",
        "minimalist", "futuristic", "dystopian", "utopian", "mythic", "folklore", "fairy tale", "storybook", "celestial", "cosmic",
        "astral", "neon", "techwear", "streetwear", "glam", "pinup", "burlesque", "cabaret", "circus", "carnival",
        "festival", "masquerade", "opera", "ballroom", "royal court", "ancient", "classical", "medieval", "renaissance", "industrial",
    ],
})
data.update({
    os.path.join("Locations", "Indoor", "Indoor.txt"): [
        "bedroom", "living room", "bathroom", "shower", "bathtub", "kitchen", "dining room", "hallway", "staircase", "home office",
        "hotel room", "suite", "penthouse", "apartment", "loft", "studio room", "dressing room", "walk in closet", "vanity room", "lounge",
        "bar", "club interior", "restaurant", "cafe", "library", "gallery", "museum", "theater", "backstage", "green room",
        "spa", "sauna", "steam room", "gym", "yoga studio", "dance studio", "ballroom", "conservatory", "sunroom", "atrium",
        "indoor pool", "locker room", "conference room", "meeting room", "showroom", "boutique", "salon", "beauty studio", "photo studio", "casting room",
    ],
    os.path.join("Locations", "Outdoor", "Outdoor.txt"): [
        "beach", "shoreline", "seaside", "oceanfront", "poolside", "garden", "park", "courtyard", "balcony", "terrace",
        "rooftop", "patio", "porch", "driveway", "street", "alley", "plaza", "market", "boardwalk", "pier",
        "harbor", "marina", "fountain", "statue garden", "orchard", "vineyard", "meadow", "field", "trail", "lakeside",
        "riverbank", "waterfall", "forest edge", "mountain view", "hilltop", "desert", "dunes", "cliff", "canyon", "valley",
        "outdoor cafe", "outdoor bar", "festival ground", "stadium", "amphitheater", "botanical garden", "greenhouse exterior", "farmyard", "coastal road", "sunset overlook",
    ],
    os.path.join("Locations", "Urban", "Urban.txt"): [
        "city street", "downtown", "financial district", "neon street", "alleyway", "crosswalk", "subway platform", "train station", "bus stop", "taxi stand",
        "skyscraper lobby", "office plaza", "shopping arcade", "mall", "boutique street", "rooftop bar", "parking garage", "stairwell", "bridge", "overpass",
        "underpass", "graffiti wall", "brick alley", "industrial yard", "warehouse exterior", "loading dock", "construction site", "food market", "night market", "street cafe",
        "city park", "fountain plaza", "public square", "riverwalk", "boardwalk", "harbor front", "marina walk", "city skyline", "urban rooftop", "apartment balcony",
        "fire escape", "city steps", "metro entrance", "street corner", "sidewalk", "streetlight", "newsstand", "corner store", "city gate", "city alley",
    ],
    os.path.join("Locations", "Nature", "Nature.txt"): [
        "forest", "woodland", "rainforest", "pine forest", "birch forest", "meadow", "prairie", "grassland", "valley", "mountain",
        "hill", "ridge", "cliff", "canyon", "desert", "dunes", "oasis", "riverbank", "stream", "waterfall",
        "lake", "lakeside", "pond", "wetlands", "marsh", "beach", "coast", "cove", "lagoon", "island",
        "orchard", "vineyard", "field", "wildflower field", "rocky shore", "tide pools", "cave entrance", "glacier", "snowfield", "alpine meadow",
        "sunset overlook", "forest clearing", "bamboo grove", "grove", "garden", "botanical garden", "stone path", "forest trail", "mountain trail", "riverside",
    ],
    os.path.join("Locations", "Studio", "Studio.txt"): [
        "photo studio", "cyclorama", "seamless backdrop", "paper sweep", "fabric sweep", "backdrop stand", "lighting rig", "softbox wall", "window light set", "portrait set",
        "beauty set", "fashion set", "editorial set", "product set", "minimal set", "grid wall set", "textured wall set", "corner set", "studio corner", "blackout studio",
        "white studio", "industrial studio", "loft studio", "daylight studio", "soundstage", "stage set", "backlot set", "set wall", "prop wall", "stool set",
        "chair set", "couch set", "bed set", "vanity set", "mirror set", "chair and table set", "platform set", "raised platform", "floor sweep set", "paper roll set",
        "studio floor", "studio backdrop", "studio panel", "studio screen", "studio drape", "studio curtain", "studio scrim", "studio frame", "studio riser", "studio bench",
    ],
})
data.update({
    os.path.join("Lighting", "Direction", "Direction.txt"): [
        "front light", "side light", "backlight", "rim light", "top light", "bottom light", "three quarter light", "split light", "short light", "broad light",
        "butterfly light", "loop light", "rembrandt light", "cross light", "kick light", "edge light", "fill light", "key light", "hair light", "background light",
        "left key light", "right key light", "left fill light", "right fill light", "left rim light", "right rim light", "top rim light", "bottom rim light", "overhead light", "under light",
        "left side light", "right side light", "left backlight", "right backlight", "rear light", "front rim light", "side rim light", "wrap light", "soft edge light", "hard edge light",
        "front shadow light", "side shadow light", "back shadow light", "top shadow light", "bottom shadow light", "left spill light", "right spill light", "top spill light", "bottom spill light", "ambient light direction",
    ],
    os.path.join("Lighting", "Source", "Source.txt"): [
        "softbox", "strip box", "octabox", "ring light", "beauty dish", "umbrella light", "window light", "skylight", "practical lamp", "desk lamp",
        "floor lamp", "ceiling light", "chandelier", "sconce", "neon sign", "led panel", "led tube", "fresnel light", "spotlight", "stage light",
        "flash", "strobe", "continuous light", "tungsten light", "daylight lamp", "candlelight", "fairy lights", "string lights", "light bar", "tube light",
        "projector light", "gobo light", "backdrop light", "hair light", "edge light", "rim light", "bounce light", "reflector light", "flagged light", "diffused light",
        "direct light", "indirect light", "hard light", "soft light", "ambient light", "moonlight", "sunlight", "golden hour light", "blue hour light", "city light",
    ],
    os.path.join("Lighting", "Setup", "Setup.txt"): [
        "three point lighting", "two point lighting", "one light setup", "high key lighting", "low key lighting", "butterfly lighting", "rembrandt lighting", "split lighting", "loop lighting", "broad lighting",
        "short lighting", "clamshell lighting", "rim lighting setup", "backlight setup", "silhouette setup", "ring light setup", "softbox setup", "window light setup", "natural light setup", "studio light setup",
        "flash setup", "strobe setup", "continuous setup", "mixed light setup", "ambient setup", "moody setup", "dramatic setup", "soft setup", "hard setup", "contrast setup",
        "beauty setup", "portrait setup", "fashion setup", "editorial setup", "glam setup", "boudoir setup", "product setup", "background light setup", "hair light setup", "key light setup",
        "fill light setup", "bounce setup", "reflector setup", "flag setup", "grid light setup", "gel light setup", "shadow play setup", "edge light setup", "cross light setup", "split rim setup",
    ],
    os.path.join("Lighting", "Contrast", "Contrast.txt"): [
        "high contrast", "low contrast", "soft contrast", "hard contrast", "medium contrast", "flat light", "punchy light", "moody contrast", "deep shadows", "soft shadows",
        "hard shadows", "gentle shadows", "crisp shadows", "shadow detail", "highlight detail", "balanced contrast", "dramatic contrast", "subtle contrast", "glow contrast", "halo contrast",
        "even light", "uneven light", "shadow heavy", "highlight heavy", "midtone heavy", "dark tones", "light tones", "rich blacks", "bright whites", "smooth gradients",
        "sharp falloff", "soft falloff", "fast falloff", "slow falloff", "shadow falloff", "highlight rolloff", "specular highlights", "diffuse highlights", "matte highlights", "glossy highlights",
        "shadow separation", "highlight separation", "shadow depth", "highlight depth", "contrast edge", "contrast curve", "contrast balance", "contrast ratio", "tone contrast", "light contrast",
    ],
    os.path.join("Lighting", "Time", "Time.txt"): [
        "golden hour", "blue hour", "midday sun", "morning light", "late afternoon light", "evening light", "sunset light", "sunrise light", "twilight", "dusk",
        "night light", "noon light", "overcast light", "cloudy light", "storm light", "moonlight", "starlight", "city night light", "streetlight glow", "neon glow",
        "window light morning", "window light afternoon", "window light evening", "soft daylight", "hard daylight", "diffused daylight", "shade light", "backlit sun", "front lit sun", "side lit sun",
        "late night light", "early morning light", "pre dawn light", "post sunset light", "nightfall", "daybreak", "midnight", "late evening", "late night", "early evening",
        "sunlit", "shade", "sunbeam", "sun flare", "sun glow", "warm light", "cool light", "neutral light", "ambient daylight", "ambient night",
    ],
})
data.update({
    os.path.join("Poses", "Standing", "Standing.txt"): [
        "standing pose", "contrapposto", "hip shot", "hands on hips", "arms crossed", "arms at sides", "hands clasped", "hands behind back", "hands in pockets", "one hand on hip",
        "one hand in hair", "both hands in hair", "arms raised", "arms overhead", "arms outstretched", "shoulder touch", "neck touch", "chin touch", "hand on thigh", "hand on waist",
        "hand on chest", "hand on shoulder", "hand on neck", "hand on cheek", "hand on jaw", "hand on head", "hand on back", "side stance", "front stance", "back stance",
        "three quarter stance", "profile stance", "toe point stance", "heel lift stance", "leg cross stance", "ankle cross stance", "wide stance", "narrow stance", "hip shift", "weight shift",
        "leaning stance", "wall lean", "door lean", "rail lean", "window lean", "chair lean", "over shoulder pose", "look back pose", "head tilt pose", "shoulder roll pose",
    ],
    os.path.join("Poses", "Sitting", "Sitting.txt"): [
        "sitting pose", "sitting on chair", "sitting on stool", "sitting on couch", "sitting on bed", "crossed legs", "ankle crossed", "knee crossed", "legs together", "legs apart",
        "one leg up", "both legs up", "legs tucked", "legs folded", "knees up", "knees apart", "hands on knees", "hands on lap", "hands on thighs", "hands behind back",
        "hands on chair", "hands on couch", "hand on face", "hand on chin", "hand on hair", "arms folded", "arms resting", "lean forward", "lean back", "slouch pose",
        "upright pose", "side sit", "corner sit", "edge sit", "perched sit", "floor sit", "sitting on floor", "sitting on steps", "sitting on bench", "sitting on ledge",
        "sitting on table", "sitting on counter", "sitting on railing", "sitting on armrest", "sitting on swing", "sitting on stool side", "sitting on bed edge", "sitting cross legged", "sitting with legs extended", "sitting with legs folded",
    ],
    os.path.join("Poses", "Lying", "Lying.txt"): [
        "lying pose", "lying on back", "lying on side", "lying on stomach", "reclining pose", "reclining on bed", "reclining on couch", "reclining on floor", "arms over head", "arms at sides",
        "hand on chest", "hand on stomach", "hand on thigh", "hand in hair", "hand on face", "legs bent", "legs straight", "one knee up", "both knees up", "leg cross",
        "ankle cross", "head tilt", "head turn", "look up pose", "look aside pose", "look down pose", "over shoulder pose", "back arch", "hip tilt", "leg lift",
        "knee bend", "foot point", "toe point", "arm stretch", "arm reach", "arm bend", "chin rest", "face down pose", "side curl", "fetal pose",
        "sprawl pose", "cuddle pose", "pillow hug", "pillow pose", "blanket pose", "bedside pose", "edge of bed pose", "floor lounge", "sofa lounge", "chaise lounge",
    ],
    os.path.join("Poses", "Kneeling", "Kneeling.txt"): [
        "kneeling pose", "one knee down", "both knees down", "kneeling on floor", "kneeling on bed", "kneeling on couch", "kneeling on stool", "kneeling on chair", "knees apart", "knees together",
        "hands on thighs", "hands on hips", "hands on chest", "hands on waist", "hands on face", "hands in hair", "arms raised", "arms forward", "arms out", "head tilt",
        "look up pose", "look down pose", "side glance", "back arch", "hip tilt", "toe point", "feet tucked", "feet crossed", "hands behind back", "shoulder touch",
        "neck touch", "chin touch", "hand on neck", "hand on cheek", "hand on jaw", "lean forward", "lean back", "upright kneel", "slouch kneel", "perched kneel",
        "kneeling on bench", "kneeling on steps", "kneeling on pillow", "kneeling on rug", "kneeling on grass", "kneeling on sand", "kneeling on platform", "kneeling with legs crossed", "kneeling with ankles crossed", "kneeling with arms crossed",
    ],
    os.path.join("Poses", "Movement", "Movement.txt"): [
        "walking", "slow walk", "stride", "step", "turn", "spin", "twirl", "pivot", "hair flip", "dress twirl",
        "coat swirl", "hip sway", "shoulder roll", "arm swing", "hand wave", "hand gesture", "look back walk", "over shoulder walk", "runway walk", "catwalk",
        "dance step", "dance pose", "stretch", "reach", "reach up", "reach out", "reach back", "lean step", "half turn", "full turn",
        "step forward", "step back", "side step", "cross step", "kick step", "leg swing", "arm raise", "arm drop", "hand to hair", "hand to hip",
        "hand to waist", "hand to thigh", "head turn", "chin lift", "look up motion", "look aside motion", "look down motion", "back arch motion", "hip twist", "pose transition",
    ],
})
data.update({
    os.path.join("Expressions", "Smiles", "Smiles.txt"): [
        "smile", "soft smile", "wide smile", "subtle smile", "closed mouth smile", "open mouth smile", "half smile", "gentle smile", "bright smile", "warm smile",
        "shy smile", "confident smile", "playful smile", "sweet smile", "sly smile", "side smile", "smirk", "grin", "laugh", "giggle",
        "teeth smile", "lip smile", "corner smile", "chin smile", "smile with eyes", "smile with dimples", "smile glance", "smile pose", "smile focus", "smile closeup",
        "smile profile", "smile front view", "smile side view", "smile tilt", "smile lift", "smile relax", "smile beam", "smile sparkle", "smile glow", "smile curve",
        "smile line", "smile contour", "smile shadow", "smile highlight", "smile detail", "smile expression", "smile mood", "smile energy", "smile moment", "smile look",
    ],
    os.path.join("Expressions", "Gaze", "Gaze.txt"): [
        "direct gaze", "side glance", "downward gaze", "upward gaze", "over shoulder gaze", "far gaze", "soft gaze", "intense gaze", "steady gaze", "focused gaze",
        "dreamy gaze", "distant gaze", "bold gaze", "calm gaze", "curious gaze", "playful gaze", "serious gaze", "neutral gaze", "flirty gaze", "inviting gaze",
        "left gaze", "right gaze", "front gaze", "back gaze", "profile gaze", "three quarter gaze", "gaze to camera", "gaze away", "gaze down", "gaze up",
        "gaze to side", "gaze to light", "gaze to window", "gaze to mirror", "gaze to floor", "gaze to ceiling", "gaze to horizon", "gaze to sky", "gaze to ground", "gaze to corner",
        "gaze under lashes", "gaze over lashes", "gaze through hair", "gaze behind hair", "gaze across shoulder", "gaze across body", "gaze across room", "gaze to reflection", "gaze to lens", "gaze to viewer",
    ],
    os.path.join("Expressions", "Eyes", "Eyes.txt"): [
        "half lidded eyes", "wide eyes", "soft eyes", "sharp eyes", "bright eyes", "calm eyes", "intense eyes", "sleepy eyes", "alert eyes", "relaxed eyes",
        "squint", "eye squint", "eye smile", "eye focus", "eye glance", "eye roll", "eye blink", "eye flutter", "eye sparkle", "eye shine",
        "eye highlight", "eye shadow", "eye contour", "eye shape", "eye line", "eye tilt", "eye lift", "eye drop", "eye center", "eye corners",
        "inner corner", "outer corner", "upper lid", "lower lid", "upper lash", "lower lash", "thick lashes", "thin lashes", "long lashes", "short lashes",
        "curled lashes", "straight lashes", "mascara lashes", "natural lashes", "lash lift", "lash fan", "lash line", "brow raise", "brow furrow", "brow relax",
    ],
    os.path.join("Expressions", "Mouth", "Mouth.txt"): [
        "pout", "lip pout", "lip bite", "lip press", "parted lips", "open mouth", "closed mouth", "soft mouth", "neutral mouth", "smile mouth",
        "corner lift", "corner drop", "mouth curve", "mouth line", "mouth contour", "mouth profile", "mouth silhouette", "mouth detail", "mouth focus", "lip line",
        "upper lip", "lower lip", "lip edge", "lip center", "lip highlight", "lip shadow", "lip texture", "lip crease", "lip shape", "lip volume",
        "tongue tip", "teeth show", "teeth peek", "teeth smile", "breath pose", "exhale pose", "inhale pose", "mouth open pose", "mouth closed pose", "mouth half open",
        "soft bite", "lip touch", "lip tap", "lip lick", "lip slip", "lip fold", "lip rest", "mouth relaxed", "mouth tense", "mouth smile",
    ],
    os.path.join("Expressions", "Mood", "Mood.txt"): [
        "confident mood", "playful mood", "flirty mood", "serene mood", "calm mood", "bold mood", "soft mood", "gentle mood", "sultry mood", "romantic mood",
        "dreamy mood", "mysterious mood", "cool mood", "warm mood", "cheerful mood", "joyful mood", "relaxed mood", "focused mood", "intense mood", "bright mood",
        "moody mood", "quiet mood", "shy mood", "sweet mood", "sassy mood", "fiery mood", "elegant mood", "poised mood", "graceful mood", "strong mood",
        "tender mood", "casual mood", "friendly mood", "inviting mood", "chill mood", "luxury mood", "glam mood", "fashion mood", "editorial mood", "art mood",
        "sensual mood", "intimate mood", "soft glam mood", "high glam mood", "low key mood", "high key mood", "noir mood", "sunlit mood", "night mood", "studio mood",
    ],
})
data.update({
    os.path.join("Accessories", "Jewelry", "Jewelry.txt"): [
        "necklace", "choker", "pendant", "locket", "chain", "rope chain", "beaded necklace", "pearl necklace", "collar necklace", "bib necklace",
        "earrings", "stud earrings", "hoop earrings", "drop earrings", "dangle earrings", "ear cuffs", "ear climbers", "ear chain", "bracelet", "bangle",
        "cuff bracelet", "charm bracelet", "anklet", "ring", "stacked rings", "signet ring", "cocktail ring", "toe ring", "nose ring", "septum ring",
        "body chain", "waist chain", "belly chain", "arm cuff", "arm band", "hand chain", "hair pin", "hair clip", "barrette", "tiara",
        "crown", "brooch", "pin", "badge", "medallion", "gemstone", "diamond", "ruby", "sapphire", "emerald",
    ],
    os.path.join("Accessories", "Eyewear", "Eyewear.txt"): [
        "sunglasses", "aviator sunglasses", "wayfarer sunglasses", "round sunglasses", "cat eye sunglasses", "oversized sunglasses", "tiny sunglasses", "square sunglasses", "rectangular sunglasses", "shield sunglasses",
        "sports sunglasses", "wraparound sunglasses", "mirrored sunglasses", "gradient sunglasses", "tinted sunglasses", "polarized sunglasses", "reading glasses", "eyeglasses", "round glasses", "square glasses",
        "rectangular glasses", "cat eye glasses", "browline glasses", "rimless glasses", "half rim glasses", "wire frame glasses", "acetate frames", "metal frames", "clear frames", "thick frames",
        "thin frames", "retro glasses", "vintage glasses", "designer glasses", "fashion glasses", "blue light glasses", "colored lens glasses", "clip on sunglasses", "monocle", "lorgnette",
        "goggles", "swim goggles", "ski goggles", "visor", "eye mask", "sleep mask", "glasses chain", "glasses strap", "glasses holder", "glasses case",
    ],
    os.path.join("Accessories", "Gloves", "Gloves.txt"): [
        "gloves", "fingerless gloves", "opera gloves", "wrist gloves", "arm length gloves", "elbow gloves", "lace gloves", "leather gloves", "satin gloves", "mesh gloves",
        "sheer gloves", "silk gloves", "velvet gloves", "latex gloves", "nitrile gloves", "cotton gloves", "knit gloves", "winter gloves", "driving gloves", "cycling gloves",
        "fitness gloves", "boxing gloves", "work gloves", "garden gloves", "fashion gloves", "bridal gloves", "evening gloves", "short gloves", "long gloves", "ruffle gloves",
        "cutout gloves", "stud gloves", "fringe gloves", "glovelets", "mitts", "mitten gloves", "sheer arm sleeves", "arm warmers", "hand warmers", "glove straps",
        "glove cuffs", "glove trim", "glove buttons", "glove lace", "glove mesh", "glove suede", "glove stretch", "glove wrap", "glove buckle", "glove ribbon",
    ],
    os.path.join("Accessories", "Hats", "Hats.txt"): [
        "hat", "cap", "baseball cap", "snapback cap", "trucker cap", "beanie", "beret", "fedora", "trilby", "panama hat",
        "boater hat", "bucket hat", "cloche hat", "pillbox hat", "top hat", "bowler hat", "newsboy cap", "flat cap", "visor", "sun hat",
        "wide brim hat", "cowboy hat", "cowgirl hat", "straw hat", "felt hat", "wool hat", "knit hat", "fisherman hat", "sailor cap", "military cap",
        "beret cap", "headscarf", "bandana", "hair scarf", "turban", "fascinator", "headpiece", "veil", "hood", "cowl",
        "earmuffs", "hairband", "headband", "tiara", "crown", "hair bow", "hair ribbon", "hair clip", "hair pin", "hair comb",
    ],
    os.path.join("Accessories", "Bags", "Bags.txt"): [
        "handbag", "shoulder bag", "crossbody bag", "tote bag", "clutch", "wristlet", "satchel", "hobo bag", "bucket bag", "saddle bag",
        "camera bag", "duffel bag", "backpack", "mini backpack", "belt bag", "fanny pack", "waist bag", "evening bag", "party bag", "box bag",
        "quilting bag", "chain bag", "top handle bag", "shopper bag", "beach bag", "weekender bag", "travel bag", "garment bag", "laptop bag", "messenger bag",
        "sling bag", "drawstring bag", "pouch", "coin purse", "wallet", "card holder", "makeup bag", "cosmetic bag", "vanity bag", "beauty case",
        "shopping bag", "paper bag", "mesh bag", "net bag", "straw bag", "basket bag", "leather bag", "suede bag", "canvas bag", "nylon bag",
    ],
})
data.update({
    os.path.join("BodyType", "Silhouette", "Silhouette.txt"): [
        "hourglass figure", "pear shape", "apple shape", "rectangle shape", "inverted triangle", "curvy figure", "slim figure", "athletic build", "petite build", "tall build",
        "lean build", "toned build", "voluptuous figure", "full figure", "narrow waist", "wide hips", "broad shoulders", "narrow shoulders", "long torso", "short torso",
        "long legs", "short legs", "balanced proportions", "soft curves", "defined curves", "gentle curves", "straight figure", "rounded figure", "full hips", "soft waist",
        "defined waist", "narrow hips", "wide waist", "curvy hips", "slender frame", "medium frame", "full frame", "small frame", "model figure", "glam figure",
        "classic figure", "statuesque figure", "lithe figure", "svelte figure", "supple figure", "feminine figure", "adult figure", "figure silhouette", "body silhouette", "waist to hip ratio",
    ],
    os.path.join("BodyType", "Proportions", "Proportions.txt"): [
        "long legs", "short legs", "long torso", "short torso", "long arms", "short arms", "wide hips", "narrow hips", "broad shoulders", "narrow shoulders",
        "small waist", "defined waist", "soft waist", "long neck", "short neck", "wide waist", "high hips", "low hips", "high waist", "low waist",
        "long waist", "short waist", "tall stature", "short stature", "balanced frame", "even proportions", "shoulder to hip balance", "hip to waist balance", "leg to torso balance", "arm to torso balance",
        "bust to waist balance", "waist to hip balance", "upper body balance", "lower body balance", "top heavy", "bottom heavy", "even build", "slender build", "curvy build", "lean build",
        "full bust", "small bust", "full hips", "small hips", "long stride", "short stride", "long reach", "short reach", "wide stance", "narrow stance",
    ],
    os.path.join("BodyType", "Frame", "Frame.txt"): [
        "petite frame", "small frame", "medium frame", "large frame", "slender frame", "narrow frame", "wide frame", "athletic frame", "lithe frame", "delicate frame",
        "sturdy frame", "soft frame", "lean frame", "toned frame", "compact frame", "tall frame", "short frame", "balanced frame", "light frame", "strong frame",
        "graceful frame", "poised frame", "curvy frame", "model frame", "classic frame", "feminine frame", "adult frame", "body frame", "figure frame", "waist frame",
        "hip frame", "shoulder frame", "torso frame", "leg frame", "arm frame", "upper frame", "lower frame", "full frame", "slim frame", "svelte frame",
        "shapely frame", "defined frame", "smooth frame", "soft frame line", "clean frame line", "gentle frame line", "sharp frame line", "rounded frame line", "straight frame line", "silhouette frame",
    ],
    os.path.join("BodyType", "Curves", "Curves.txt"): [
        "curves", "soft curves", "defined curves", "gentle curves", "full curves", "rounded curves", "smooth curves", "waist curve", "hip curve", "bust curve",
        "leg curve", "arm curve", "back curve", "neck curve", "shoulder curve", "rib curve", "torso curve", "body curve", "figure curve", "silhouette curve",
        "curve line", "curve contour", "curve profile", "curve silhouette", "curve shape", "curve flow", "curve balance", "curve ratio", "curve symmetry", "curve depth",
        "curve highlight", "curve shadow", "curve detail", "curve focus", "curve view", "waist to hip curve", "bust to waist curve", "hip to thigh curve", "back arch curve", "hip sway curve",
        "glute curve", "thigh curve", "calf curve", "ankle curve", "wrist curve", "neckline curve", "jaw curve", "cheek curve", "chest curve", "stomach curve",
    ],
    os.path.join("BodyType", "Height", "Height.txt"): [
        "petite height", "short height", "average height", "tall height", "statuesque height", "model height", "long leg height", "compact height", "medium height", "very tall",
        "very short", "tall stature", "short stature", "medium stature", "high stature", "low stature", "slender height", "curvy height", "athletic height", "balanced height",
        "long line height", "short line height", "extended height", "compressed height", "upright height", "poised height", "graceful height", "elegant height", "classic height", "modern height",
        "towering height", "mini height", "mid height", "full height", "half height", "three quarter height", "long form height", "short form height", "vertical height", "vertical line",
        "height emphasis", "height focus", "height detail", "height proportion", "height balance", "height profile", "height silhouette", "height view", "height line", "height contour",
    ],
})

for path, entries in data.items():
    if len(entries) < 50:
        raise SystemExit(f"{path} has {len(entries)} entries")
    if len(entries) != len(set(entries)):
        raise SystemExit(f"{path} has duplicate entries")

for rel_path, entries in data.items():
    abs_path = os.path.join(root, rel_path)
    os.makedirs(os.path.dirname(abs_path), exist_ok=True)
    content = header + "\n" + "\n".join(entries) + "\n"
    with open(abs_path, "w", encoding="utf-8") as f:
        f.write(content)

print(f"Wrote {len(data)} files under {root}")
