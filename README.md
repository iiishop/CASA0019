# Study Space Availability & Comfort Monitoring  
**Physical–Digital Data Visualisation System**  
CASA0019 – Sensor Data Visualisation, UCL

A hybrid physical–digital system that visualises study space availability and perceived comfort conditions using a shared visual language, combining an ambient tabletop device with a real-time digital twin.

---

## Overview

Choosing a suitable study space is not only about availability, but also about atmosphere: how busy, noisy, or comfortable a space feels. At UCL, study space availability is currently communicated through a web-based booking system that prioritises scheduling efficiency but provides limited insight into spatial and experiential qualities.

This project reinterprets existing availability data and simulated room conditions through **visual behaviour rather than numerical representation**. Instead of replacing the current booking system, it introduces an alternative way of *perceiving* space performance—supporting fast, low-effort decision-making through an ambient physical device and a complementary digital twin.

---

## System Architecture

The system consists of three tightly integrated layers:

1. **Data Layer**
   - Study space availability derived from the UCL Library booking API
   - Environmental and behavioural indicators (occupancy, noise, lighting, temperature) generated as relative perceptual signals for prototyping

2. **Communication Layer**
   - MQTT used as a shared messaging backbone
   - Physical device and digital twin subscribe to identical topics, ensuring consistency

3. **Visualisation Layer**
   - A physical tabletop device for glance-based awareness
   - A digital twin for inspection, comparison, and persistence

Both interfaces operate on the same data and visual rules, differing only in how information is presented.

---

## Data & Visual Encoding

Rather than displaying raw values, the system translates data into a **coherent visual grammar** designed for intuitive perception.

### Availability Encoding
- Daily timeline from **09:00–21:00**
- 24 segments representing 30-minute intervals
- **Green** = free, **Red** = booked

### Comfort & Room Dynamics
Four attributes are visualised as relative signals:
- Occupancy
- Noise
- Lighting
- Temperature

In **Condition mode**, attributes are displayed sequentially:
- Colour identifies the active attribute
- Quantity (number of illuminated segments) encodes intensity
- Temporal behaviour (rotation and progressive fill) conveys change over time

This approach prioritises sense-making through observation rather than analytical reading.

---

## Physical Interface

The physical artefact is designed as a **passive, ambient interface** that communicates information without demanding focused attention.

- Circular tabletop form inspired by collaborative learning environments
- One study space displayed at a time
- Rotary encoder interaction:
  - Rotate to switch spaces
  - Press to switch modes

### Visual Components
- **NeoPixel LED ring**
  - Bookings mode: full-day availability timeline
  - Condition mode: animated room dynamics
- **Acrylic overlay**
  - Watch-face–like time markings to anchor temporal interpretation
- **TFT display**
  - Contextual information in bookings mode
  - Expressive icons in condition mode (no numbers)

The device is intentionally readable without instruction, relying on colour, position, repetition, and motion.

---

## Digital Twin

The digital twin extends the physical device into a digital environment without redefining its visual language.

- Subscribes to the same MQTT topics as the physical device
- Preserves colour mappings, timing, and behavioural logic

### What the Digital Twin Adds
- Parallel display of all comfort attributes using concentric rings
- Persistent visual states for reflection and comparison
- Quantitative values and units when precision is required

The digital twin is not a dashboard; it is an alternative reading of the same visual grammar, supporting deeper inquiry while maintaining continuity with the physical interface.

---

## Physical–Digital Consistency

A core design principle is **visual continuity across media**.

- Colour = attribute identity
- Quantity = intensity
- Motion = change over time

Because these rules remain stable, users can move between physical and digital representations without reinterpreting the data. This reduces cognitive load and supports rapid understanding.

---

## Evaluation

Evaluation focused on **readability and perceptual clarity** rather than numerical accuracy.

Key observations:
- Circular timelines strongly support time-based reasoning
- Colour contrast enables rapid recognition from a distance
- Animated intensity effectively communicates relative comfort levels
- The digital twin revealed clarity issues not visible in the physical artefact alone, informing refinement

Overall, the system supports quick judgement of suitability rather than detailed explanation.

---

## Limitations & Future Work

While expressive, the system highlights the tension between visual richness and cognitive simplicity. LED brightness and animation speed require careful calibration to remain comfortable in quiet study environments.

Future extensions include:
- Softer diffusion and lower baseline brightness
- Slower, more atmospheric transitions
- Enabling booking interaction via the physical device:
  - Slot selection through the rotary encoder
  - Booking intent published via MQTT
  - Validation through backend services
  - Completion via official institutional workflows
  - Feedback through brief visual animations

---

## Team

- **Gilang Pamungkas** — Physical device programming, MQTT communication, system integration  
- **Yuqian Lin** — Digital twin and AR visualisation (Unity)  
- **Chaoshuo Han** — Structural design and 3D modelling  
- **Cheng Zhong** — Cross-module integration and system coordination  

---

## Repository Structure

```text
hardware/
├── fritzing/
│   └── wiring.fzz
├── enclosure/
│   ├── fusion360/
│   └── stl/
firmware/
├── esp32/
digital-twin/
├── unity/
docs/
├── images/
├── booklet/

 ```

---
## Acknowledgements

 Developed as part of CASA0019 – Sensor Data Visualisation
UCL Centre for Advanced Spatial Analysis, University College London.