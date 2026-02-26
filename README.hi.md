<p align="center">
  <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.md">English</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center"><img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/DeterministicMouseTrainingEngine/readme.png" alt="Deterministic Mouse Training Engine" width="400"></p>

<p align="center"><strong>Deterministic 60Hz mouse training engine — fixed timestep, alpha interpolation, virtual coordinate space, pluggable game modes.</strong></p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow.svg" alt="License: MIT"></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-8-purple.svg" alt=".NET 8"></a>
  <a href="https://mcp-tool-shop-org.github.io/DeterministicMouseTrainingEngine/"><img src="https://img.shields.io/badge/Landing_Page-live-blue" alt="Landing Page"></a>
</p>

---

यह .NET 8 MAUI (विंडोज-आधारित) पर बनाया गया है, जिसमें एक पूरी तरह से नियतात्मक, निश्चित-समय अंतराल वाला सिमुलेशन, कंपोज़ेबल ब्लूप्रिंट म्यूटेटर और प्लेटफ़ॉर्म-स्थिर रन आइडेंटिटी है।

---

## आर्किटेक्चर

चार मॉड्यूल वाला मॉनोलिथ। कोई चक्र नहीं, लाइब्रेरी में प्लेटफ़ॉर्म से कोई डेटा लीक नहीं।

```
MouseTrainer.Domain        --> (nothing)          Shared primitives, RNG, run identity
MouseTrainer.Simulation    --> Domain             Deterministic loop, modes, mutators, levels
MouseTrainer.Audio         --> Domain             Cue system, asset verification
MouseTrainer.MauiHost      --> all three          Composition root, MAUI platform host
```

पूर्ण निर्भरता ग्राफ और संवैधानिक नियमों के लिए [`docs/modular.manifesto.md`](docs/modular.manifesto.md) देखें।

---

## गेम मोड

### रिफ्लेक्सगेट्स

साइड-स्क्रॉलिंग गेट चुनौती। ऊर्ध्वाधर दीवारों पर दोलन करने वाले छिद्र - कर्सर को प्रत्येक गेट से गुजारें इससे पहले कि स्क्रॉल आपको पकड़ ले। नियतात्मक सीड → हर बार समान स्तर।

- 60 हर्ट्ज़ का निश्चित टाइमस्टेप, जिसमें एक्यूमुलेटर-आधारित कैच-अप है।
- प्लेटफ़ॉर्म-स्थिर पीढ़ी के लिए `xorshift32` RNG को प्रत्येक रन के लिए सीड किया गया है।
- रन आइडेंटिटी के लिए FNV-1a 64-बिट हैशिंग (समान सीड + मोड + म्यूटेटर = हर जगह समान रनआईडी)।

---

## ब्लूप्रिंट म्यूटेटर

छह कंपोज़ेबल ट्रांसफॉर्म जो खेलने से पहले उत्पन्न स्तरों को बदलते हैं। इन्हें `LevelBlueprint` पर एक क्रमबद्ध तरीके से लागू किया जाता है:

| म्यूटेटर | मुख्य पैरामीटर | प्रभाव |
|---------|-----------|--------|
| **NarrowMargin** | `pct` ∈ [0,1] | छिद्रों की ऊंचाई को कम करता है — तंग अंतराल। |
| **WideMargin** | `pct` ∈ [0,1] | छिद्रों की ऊंचाई को बढ़ाता है — अधिक लचीला। |
| **DifficultyCurve** | `exp` ∈ [0.1,5] | इंडेक्स द्वारा गेट की कठिनाई को पुनः मैप करता है — शुरुआत में कठिन या अंत में कठिन। |
| **RhythmLock** | `div` ∈ {2,3,4,6,8} | N विभाजनों में गेट चरणों को क्वांटीकृत करता है — लयबद्ध पैटर्न। |
| **GateJitter** | `str` ∈ [0,1] | sin() के माध्यम से नियतात्मक ऊर्ध्वाधर ऑफ़सेट — स्थानिक गड़बड़ी। |
| **SegmentBias** | `seg`, `amt`, `shape` | गेट्स को खंडों में विभाजित करता है, जिसमें प्रत्येक खंड में कठिनाई का पूर्वाग्रह होता है। |

म्यूटेटर शुद्ध फ़ंक्शन हैं: `LevelBlueprint → LevelBlueprint`. वे पाइपलाइन के माध्यम से कंपोज़ होते हैं (`specs.Aggregate`), `MutatorRegistry` से फैक्ट्री-रिज़ॉल्व किए जाते हैं, और उनके पैरामीटर को पुनरुत्पादन के लिए `RunId` हैश में जमा किया जाता है।

### सेगमेंटबाइस शेप्स

- **क्रेसेन्डो** (shape=0): आसान शुरुआत → कठिन अंत। `d = 2t - 1`
- **वैल्यू** (shape=1): कठिन मध्य, आसान अंत। `d = 8t(1-t) - 1`
- **वेव** (shape=2): वैकल्पिक रूप से आसान/कठिन खंड। `d = (-1)^k`

---

## परियोजना संरचना

```
src/
  MouseTrainer.Domain/          Leaf module — events, input, runs, RNG
    Events/                     GameEvent, GameEventType
    Input/                      PointerInput
    Runs/                       RunDescriptor, RunId, MutatorId/Spec/Param, ModeId, DifficultyTier
    Utility/                    DeterministicRng (xorshift32)

  MouseTrainer.Simulation/      Deterministic simulation engine
    Core/                       DeterministicLoop, FrameResult, IGameSimulation
    Debug/                      ISimDebugOverlay
    Levels/                     LevelBlueprint, ILevelGenerator, LevelGeneratorRegistry
    Modes/ReflexGates/          Gate, ReflexGateSimulation, ReflexGateGenerator, ReflexGateConfig
    Mutators/                   IBlueprintMutator, MutatorPipeline, MutatorRegistry, 6 mutators
    Session/                    SessionController, SessionModels

  MouseTrainer.Audio/           Audio cue system
    Assets/                     AssetManifest, AssetVerifier, IAssetOpener
    Core/                       AudioDirector, AudioCue, AudioCueMap, IAudioSink

  MouseTrainer.MauiHost/        MAUI composition root (Windows)

tests/
  MouseTrainer.Tests/           214 tests across 6 categories
    Architecture/               Dependency boundary enforcement
    Determinism/                Replay regression, RNG, session controller
    Levels/                     Generator extraction
    Mutators/                   Blueprint mutator correctness + composition
    Persistence/                Session store
    Runs/                       RunDescriptor golden hashes + identity

tools/
  MouseTrainer.AudioGen/        Audio asset generation tooling

docs/
  modular.manifesto.md          Dependency graph + constitutional rules
  MAUI_AssetOpener_Snippet.md   Platform asset wiring snippet
```

---

## बिल्ड और टेस्ट

```bash
# Build simulation library (0 warnings, TreatWarningsAsErrors)
dotnet build src/MouseTrainer.Simulation/

# Run all 214 tests
dotnet test tests/MouseTrainer.Tests/

# Run MAUI host (Windows — use Visual Studio, set startup to MauiHost)
```

---

## मुख्य डिज़ाइन सिद्धांत

- **नियतिता मौलिक है।** समान सीड → समान सिमुलेशन → समान स्कोर, हमेशा। कोई `DateTime.Now`, कोई `Random`, कोई प्लेटफ़ॉर्म-निर्भर फ़्लोट "हॉट पाथ" में नहीं।
- **मॉड्यूलर मॉनोलिथ, माइक्रोसर्विस नहीं।** चार असेंबली, जिसमें एक-तरफ़ा निर्भरता लागू की गई है। डोमेन सबसे निचले स्तर पर है; MauiHost एकमात्र कंपोज़िशन रूट है।
- **प्रोटोकॉल-ग्रेड आइडेंटिटी।** `MutatorId`, `ModeId`, `RunId` स्थायी हैं — एक बार बनाए जाने पर, वे हमेशा के लिए स्थिर रहते हैं। FNV-1a हैशिंग जिसमें मानक पैरामीटर सीरियललाइज़ेशन शामिल है।
- **चेतावनी त्रुटियां हैं।** लाइब्रेरी प्रोजेक्ट `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` का उपयोग करते हैं। MAUI होस्ट इसमें ऑप्ट-आउट करता है (SDK-जनरेटेड चेतावनियाँ)।

---

## लाइसेंस

[MIT](LICENSE)

> [MCP Tool Shop](https://mcp-tool-shop.github.io/) द्वारा बनाया गया।
