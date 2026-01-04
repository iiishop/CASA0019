# Study Space Availability & Comfort Monitoring  
**CASA0019 – Sensor Data Visualisation**  
UCL Centre for Advanced Spatial Analysis (CASA)

A physical–digital data visualisation system that reinterprets UCL study space availability and room conditions through a shared visual language, combining an ambient tabletop device with a digital twin.

---

## Project Overview

Bookable study spaces play an important role in shaping how students focus, interact, and work together. These spaces support individual concentration as well as group discussion and collaborative learning, meaning that their availability and atmosphere directly influence both personal productivity and collective engagement.

At UCL, a web-based reservation system allows students to check the availability of study spaces in advance. While effective for scheduling, this system represents space primarily through box-style time slots and simplified visual layouts. Such representations provide limited insight into the spatial, social, and experiential qualities of learning environments. As a result, students often still need to physically visit a space to assess whether it supports their current mode of work. At an operational level, the same representation also makes it difficult to quickly perceive how spaces respond to changing occupancy patterns and room conditions throughout the day.

This project does not aim to replace the existing system, but to reinterpret its data through visualisation. It investigates how study space availability and room dynamics can be communicated through a combination of physical data visualisation and a digital twin, enabling information to be perceived passively rather than actively interpreted. By expressing data through physical form and visual behaviour, the project frames visualisation as a process of sense-making rather than optimisation. This approach supports everyday student decision-making while also offering facilities teams a higher-level understanding of how learning spaces perform over time.

---

## Team

- **Gilang Pamungkas** — Physical device programming and data communication (MQTT)  
- **Yuqian Lin** — Digital twin and AR visualisation (Unity)  
- **Chaoshuo Han** — Structural design and 3D modelling of the physical device  
- **Cheng Zhong** — Cross-module integration, digital twin support, and report structure  

Repository: https://github.com/iiishop/CASA0019

---

## Context & Site

The system focuses on bookable study spaces at **UCL East Library**, where students regularly balance availability, comfort, and social atmosphere when choosing where to work.

![UCL East Library and study space locations](docs/images/ucl_east_library.png)

---

## From Data to Visual Language

Rather than presenting raw metrics, the project translates key spatial conditions into a visual language that can be perceived intuitively. The system combines two types of data: availability information derived from the UCL Library booking API, and a set of environmental and behavioural indicators designed to explore how the atmosphere of learning spaces can be communicated visually rather than numerically.

Availability is converted into a 30-minute free/booked timeline for each study space across the day (09:00–21:00). This representation supports rapid comparison without requiring users to read or interpret detailed schedules.

To represent comfort and room dynamics, the project focuses on four indicators: occupancy, noise, lighting, and temperature. In the current prototype, these values are synthetically generated within plausible ranges and treated as relative perceptual signals rather than precise measurements.

The system uses a shared set of visual rules across physical and digital representations. In Bookings mode, the NeoPixel ring acts as a daily timeline where green indicates free slots and red indicates booked slots. In Condition mode, the display cycles through occupancy, noise, temperature, and lighting. Colour identifies the active attribute, while the number of illuminated LEDs encodes relative intensity. Meaning emerges through temporal behaviour rather than labels or numerical values.

---

## Physical Data Visualisation Device

The physical data visualisation device is conceived as a passive, ambient interface that communicates the state of study spaces without requiring sustained attention or analytical interaction. Rather than functioning as a dashboard that users must actively query, the device supports quick, intuitive readings through form, colour, and movement.

![Wiring diagram](docs/images/wiring_diagram.png)  
![Fusion 360 enclosure design](docs/images/fusion360_design.png)

The device adopts a tabletop form factor with a circular geometry, inspired by collaborative learning environments. The enclosure was designed in Fusion 360 and fabricated through 3D printing. One study space is displayed at a time, with users navigating between spaces by rotating a rotary encoder and switching between modes by pressing the encoder. This interaction model prioritises clarity and calm, sequential exploration.

A NeoPixel LED ring serves as the primary visual medium. In Bookings mode, the ring represents the full day using 24 LEDs, each corresponding to a 30-minute interval. An acrylic overlay engraved to resemble a watch face anchors the abstract LED timeline in a familiar time-reading convention.

In Condition mode, the LED ring shifts to an animated display of room dynamics. Colour identifies the active attribute, while progressive illumination communicates relative intensity. Motion and repetition allow users to perceive change over time without relying on numerical values.

A TFT screen plays a complementary role. In Bookings mode, it displays contextual information such as room name, capacity, facilities, and daily booking percentage. In Condition mode, the screen avoids numerical data and instead presents an expressive icon summarising the room’s overall state.

![TFT emotive display](docs/images/tft_emotive.png)

---

## Digital Visualisation & Digital Twin

The digital twin extends the physical data visualisation device into a complementary digital medium. While the physical device is designed for peripheral, glance-based awareness, the digital twin supports closer inspection and comparison without altering the underlying data or visual logic.

![Digital twin interface](docs/images/digital_twin.png)

Both the physical device and the digital twin subscribe to the same MQTT topics and operate on identical data streams. This ensures consistency across media, preserving colour mappings, modes, and temporal behaviour.

The digital twin presents all four comfort indicators simultaneously using concentric rings, enabling parallel reading and comparison. Quantitative values and units are displayed to support verification when precision is required. Unlike the physical device, the digital twin provides persistence, allowing users to pause, compare, and reflect on room conditions over time.

---

## Physical–Digital Integration

A core design principle of the project is the maintenance of a consistent visual language across physical and digital representations. Colour distinguishes attributes, quantity represents intensity, and motion communicates change over time. By preserving these mappings across media, the system supports cognitive continuity and reduces the need for relearning when users move between physical and digital interfaces.

---

## Development Process & Visual Iteration

The project evolved through iterative testing of how much information could be communicated visually without overwhelming users. Early designs that displayed multiple spaces simultaneously were abandoned due to physical complexity and cognitive overload. Refocusing on a single space at a time improved legibility and interpretability.

Rather than introducing numerical summaries, the system adopts emotive icons to represent overall room state, encouraging intuitive interpretation. Legends and scale explanations were externalised into a separate visual booklet to preserve the calm, ambient quality of the device.

![Visual language booklet](docs/images/visual_booklet.png)

---

## Evaluation: Readability & Perception

Evaluation focused on perceptual clarity and interpretability rather than numerical accuracy. The circular LED timeline proved highly legible for availability, aligning naturally with time-based mental models. Strong colour contrast enabled recognition from a distance, while progressive illumination supported relative comparison of comfort conditions.

The digital twin played an important role in evaluation by revealing limitations not visible in the physical artefact alone, such as moments where colour similarity or animation speed reduced clarity. Overall, the system prioritises perceptual coherence over analytical precision, supporting everyday judgement of space suitability.

---

## Reflection & Future Extensions

The project highlights the tension between visual richness and cognitive simplicity. While colour, motion, and spatial encoding effectively communicate presence and intensity, they also require careful calibration to avoid overload. Future iterations would focus on softer LED diffusion, lower baseline brightness, and slower transitions to enhance visual comfort.

A key future extension is to move beyond observing availability toward enabling booking directly through the system. This would allow users to select time slots via the physical device, publish booking intent through MQTT, validate availability through backend services, and complete booking via official institutional workflows, with feedback communicated through brief visual animations.

![Booking interaction concept](docs/images/booking_concept.png)

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

---

## Acknowledgements 

Developed as part of CASA0019 – Sensor Data Visualisation
UCL Centre for Advanced Spatial Analysis, University College London.