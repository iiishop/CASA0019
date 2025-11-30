Project Title: Study Space Availability and Comfort Monitoring
UCL CASA0019 — Sensor Data Visualisation

Group Members: [Names]
Repository Link: [URL]

1. Introduction & Project Context

University study spaces play a crucial role in how students focus, collaborate, and learn. However, students often face challenges in navigating these spaces — seats may be full, noise levels unpredictable, and comfort conditions unclear until they physically arrive. As universities move toward smart-campus infrastructures, ambient IoT visualisation systems can help students make informed decisions immediately, without needing to open an app or interpret complex dashboards.

This project proposes a connected physical–digital ecosystem designed to communicate study-space conditions at a glance.
Our system integrates:

A physical tabletop data device using NeoPixel LEDs and TFT displays

A Unity Digital Twin mirroring live data

The UCL Library Booking API for real-time availability

Simulated environmental + behavioural data to model comfort

The aim is to create a novel, ambient visualisation device that is:

Emotionally expressive — conveying the “mood” of a study space

Low cognitive load — instantly interpreted without reading

Multi-modal — blending physical ambience with digital detail

Replicable — providing a template for smart-campus IoT design

Our final design uses five NeoPixel rings arranged on a wooden meeting-room-style tabletop, each with a 1.8-inch TFT screen at the centre. The round layout intentionally resembles a collaborative meeting setup, allowing viewers to “sense” a study-space ambience simply by looking at the installation from afar.

2. Rationale & Design Ideation
Why an Ambient Display?

Surveying existing occupancy dashboards showed that students prefer immediate, non-textual signals about seat availability and comfort. Emotional cues and colour are interpreted far faster than numerical data.

Why a Wooden Meeting-Table Metaphor?

Earlier prototypes focused on generic boxes or flat LED panels, but they lacked emotional resonance. The meeting-room style tabletop offered:

A stronger connection to real study environments

Warmth and familiarity through materials

A natural circular layout for five study rooms

A physical metaphor for collaboration and gathering

This aesthetic choice significantly improves the narrative clarity of the device.

Why NeoPixel Rings + TFT Displays?

Our final visualisation strategy emerged after testing multiple mediums:

Prototype	Issue
8×8 / 16×16 LED matrices	Too blocky for emotion
Full 96×48 RGB matrix	Clear, but loses ambience & physicality
Pure NeoPixel rings	Good ambience, limited detail
Split-flap mechanical	Too complex to fabricate

NeoPixel rings + TFT screens provided the perfect hybrid:

NeoPixel rings → represent individual indicators in 4 quadrants

TFT screens → display expressive 128×128 emoticons or room details

Wooden tabletop → brings the installation into a familiar spatial metaphor

Thus, the design evolved toward a multi-layered communication system.

3. Data Sources & Logic
3.1 UCL Library Booking API (Primary Data Feed)

The system retrieves real-time information for five study spaces at UCL East Library:

Room availability (Available / Busy / Full)

Maximum capacity

Room metadata (floor, building, facilities)

Update timestamp

This forms the backbone of Availability Mode, feeding directly into both the physical device and the digital twin.

3.2 Environmental & Behavioural Condition Simulation (Four Indicators)

To model comfort and behavioural dynamics in study rooms, we simulate four key indicators:

1. Noise Level (0–100)

Models conversational and ambient noise

Drives LED pulsing speed

Heavy noise increases emoticon stress level

2. Light Level (low → high)

Represents brightness suitability for studying

LED quadrants shift between cool/warm tones

3. Temperature (°C)

Maps to colours: blue → green → orange → red

Affects comfort emoticon (e.g., ❄️ or 🔥)

4. Door Activity / Footfall Count

This is our most important behavioural indicator.

Measures how frequently the room is entered/exited

High activity = more distractions

Strongly impacts perceived comfort

Mapped to LED flicker effects

The inclusion of door-movement frequency reflects real student experience — a room that is technically available may still feel uncomfortable if people constantly enter or leave.

3.3 Data Pipeline
UCL API → Unity/Node → ESP32 → TFT Display + NeoPixel Rings  
Simulated Condition Data → ESP32 → LED Quadrants + Emoticon  
Unity → Mirrors entire logic for digital twin

4. Physical Data Device (Final Design)
4.1 Form & Aesthetic

The device uses:

A wooden tabletop surface (laser-cut finish)

Three wooden legs for elevation

A rounded arrangement of five study-space nodes

Each node includes:

1 × NeoPixel ring (24 LEDs)

1 × ST7735S TFT display (128×160 pixels) embedded in the centre

The form resembles a miniature collaborative meeting table, symbolising the study spaces it represents.

4.2 Visualisation Modes
A. Availability Mode (API-driven)

Each TFT screen displays a compact room-information card:

Room name

Maximum capacity

Key facilities (plug points, enclosed pod, laptop charging)

Building location

Floor

Accessibility link

This mode serves as a spatial directory of UCL East study pods.

LED rings provide a halo-style availability cue:

Green → Available

Yellow → Limited

Red → Full

Ripple animation on refresh

B. Condition Mode (Comfort Summary)

The TFT screen shows a 128×128 expressive emoticon that summarises all four indicators:

Example mappings:

🙂 Comfortable

😐 Neutral

😬 Busy

😣 Noisy / Distracting

😴 Very Quiet

❄️ Too Cold

🔥 Too Hot

⛔ Closed / Unavailable

This combines environmental + behavioural indicators into an immediate emotional cue.

The NeoPixel ring’s four quadrants light up separately to show:

Quadrant	Indicator
Top-left	Noise level
Top-right	Light level
Bottom-right	Temperature
Bottom-left	Door activity

This dual-layer visualisation (LED detail + TFT summary) provides both quick emotional reading and analytical breakdown.

4.3 Interaction Design

Two buttons on the device control:

Mode switching (Availability ↔ Condition)

Date navigation (for replaying snapshots, optional)

A boot animation indicates data loading.
The device is designed for passive interaction — users understand the content simply by observing.

4.4 Technical Implementation

ESP32 controls all 5 NeoPixel rings + 5 TFT screens

SPI optimization ensures tear-free rendering on TFT

LED brightness is tuned for indoor viewability

Quadrant logic converts raw values into visual narratives

Condition scoring merges 4 indicators into a single emoticon

4.5 What Worked Well

The tabletop metaphor made the concept instantly recognisable

NeoPixel quadrants create high information density with low clutter

Emotional icons increased intuitiveness

TFT screens elevated the expressiveness beyond LEDs alone

Wood material improved visual appeal and familiarity

4.6 Issues & Improvements

Power delivery required careful distribution (TFT + LEDs draw spikes)

SPI redraw speed needed optimisation for multi-screen use

Higher-quality capacitive buttons could improve interaction

LED–TFT brightness matching required visual calibration

5. Unity Digital Twin (Extended Digital Interface)
5.1 Purpose

The digital twin enhances the physical device by providing:

Deeper context

Historical trends

Detailed room information

Explicit numeric sensors

Interactivity

This dual-representation demonstrates how physical ambient devices and detailed dashboards can complement each other.

5.2 Features

Real-time data syncing with physical device

Historical trend graphs

Interactive room panels

Comfort breakdown

Colour and emoticon mapping identical to the physical device

Animation transitions for clarity

5.3 Architecture

Unity C# RoomManager component

JSON feeds from API + simulation

Prefab-based UI blocks

Shared colour logic with ESP32

Scene hierarchy matching physical layout

5.4 Reflections

What worked:

Smooth visual hierarchy

Strong alignment with physical device

Clear navigation

Replicable structure

Challenges:

Prefab linking bugs

Time constraints for MQTT

Need for automated data logging

6. Physical–Digital Integration

We emphasised cohesion across both components:

Integration Principle	Implementation
Shared colour logic	Same state → same colour on both devices
Shared emoticons	128×128 icons reused in Unity
Shared data model	One JSON structure powering both
Shared layout	Five nodes arranged in the same order
Mirrored modes	Availability and Condition behave the same

The result feels like one system with two interfaces.

7. Methodology & Reproducibility

Steps:

Early sketches

Low-fidelity cardboard prototypes

TFT/LED integration tests

Pixel icon design (128×128)

API testing and structuring

Comfort simulation scripting

Unity environment build

Material fabrication

System integration

Documentation

Everything required to reproduce the project is included in this repository:

Wiring diagrams

Code with comments

Icon assets

Unity project

Data logic documentation

8. Individual Contributions

(Replace with your group details)

Example structure:

Member A — Hardware + Electronics

Member B — API + Data Logic

Member C — Unity Digital Twin

Member D — Fabrication + Documentation

9. Future Extensions

Real sensors (mmWave, CO₂, sound classification)

ML predictive availability

WebGL export

Multi-library network

Fully wireless ESP32-MQTT communication

Adaptive brightness + auto-comfort scoring

10. Conclusion

This project demonstrates how ambient IoT visualisation, expressive emotional design, and physical–digital integration can transform the way students perceive and navigate study spaces. By combining NeoPixel detail with emotive TFT displays inside a wooden tabletop metaphor, the system communicates both analytic data and the emotional “feel” of a room. The Unity digital twin extends the functionality, providing depth and clarity.

Together, these components illustrate a future direction for smart-campus design: one where information is not merely displayed, but felt.