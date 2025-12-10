/****************************************************
 * UCL Study Space Visualiser
 * Rotary Encoder Version
 * - Rotate encoder to change room
 * - Push encoder to change mode (Bookings / Condition)
 *
 * Bookings mode:
 *  - NeoPixel: 24 slots (09:00–21:00, 30 mins each)
 *              red = booked, green = free
 *  - TFT: room details (name, capacity, facilities)
 *
 * Condition mode:
 *  - TFT: expressive emoji based on "state"
 *  - NeoPixel: animated bar for 4 attributes
 *              occupancy / noise / temperature / light
 ****************************************************/

#include <SPI.h>
#include <WiFiNINA.h>
#include <PubSubClient.h>
#include <ArduinoJson.h>
#include <Adafruit_GFX.h>
#include <Adafruit_ST7735.h>
#include <Adafruit_NeoPixel.h>
#include "arduino_secrets.h"

// --------------------------------------------------
// TFT ST7735
// --------------------------------------------------
#define TFT_CS   5
#define TFT_DC   7
#define TFT_RST  6

Adafruit_ST7735 tft = Adafruit_ST7735(TFT_CS, TFT_DC, TFT_RST);

const int CX = 64;
const int CY = 70;

// --------------------------------------------------
// NEOPIXEL RING (24x SK6812 RGBW)
// --------------------------------------------------
#define NEOPIXEL_PIN 1
#define LED_COUNT    24

// Ring is GRBW
Adafruit_NeoPixel strip(LED_COUNT, NEOPIXEL_PIN, NEO_GRBW + NEO_KHZ800);

// --------------------------------------------------
// ROTARY ENCODER PINS
// --------------------------------------------------
#define ENC_CLK 2
#define ENC_DT  3
#define ENC_SW  4

int  lastClk     = HIGH;
bool lastButton  = HIGH;
unsigned long lastButtonTime = 0;
const unsigned long BUTTON_DEBOUNCE = 180;

// --------------------------------------------------
// WIFI + MQTT
// --------------------------------------------------

// WiFi credentials + MQTT_* come from arduino_secrets.h

const char* MQTT_BASE = "student/CASA0019/Gilang/studyspace";

WiFiClient wifiClient;
PubSubClient mqttClient(wifiClient);

// --------------------------------------------------
// ROOMS & METADATA
// --------------------------------------------------
const uint8_t ROOM_COUNT = 5;

const char* ROOM_IDS[ROOM_COUNT] = {
  "24380",
  "24381",
  "24382",
  "24546",
  "24547"
};

const char* ROOM_NAMES[ROOM_COUNT] = {
  "Pod 216",
  "Pod 217",
  "Group 218",
  "Pod 212A",
  "Pod 212B"
};

const char* ROOM_DETAILS[ROOM_COUNT] = {
  "Single Study Pod 216\n"
  "Capacity: 1\n"
  "Facilities: Plug, PC,\nheight adj. desk\n"
  "Building: UCL East\nLibrary 2nd Floor\n"
  "Features:\nLaptop charging\nEnclosed pod",

  "Single Study Pod 217\n"
  "Capacity: 1\n"
  "Facilities: Plug, PC,\nheight adj. desk\n"
  "Building: UCL East\nLibrary 2nd Floor\n"
  "Features:\nLaptop charging\nEnclosed pod",

  "Group Study Room 218\n"
  "Capacity: 6\n"
  "Facilities: Plug,\nmonitor for laptop\n"
  "Building: UCL East\nLibrary 2nd Floor\n"
  "Features:\nLaptop charging\nEnclosed room",

  "Single Study Pod 212A\n"
  "Capacity: 1\n"
  "Facilities: Plug\n"
  "Building: UCL East\nLibrary 2nd Floor\n"
  "Features:\nLaptop charging\nEnclosed pod",

  "Single Study Pod 212B\n"
  "Capacity: 1\n"
  "Facilities: Plug\n"
  "Building: UCL East\nLibrary 2nd Floor\n"
  "Features:\nLaptop charging\nEnclosed pod"
};

// --------------------------------------------------
// ROOM DATA STRUCTURE
// --------------------------------------------------
struct RoomData {
  bool   hasStatus = false;
  float  occupancy = 0;
  float  noise = 0;
  float  temperature = 0;
  float  light = 0;
  String state = "neutral";

  bool   hasTimeline = false;
  int    timelineLen = 0;
  bool   slotBooked[LED_COUNT];
};

RoomData rooms[ROOM_COUNT];

// --------------------------------------------------
// UI STATE
// --------------------------------------------------
bool   timelineMode    = true;   // true = Bookings
int    selectedRoom    = 0;

unsigned long lastAttrChange = 0;
unsigned long lastAnimStep   = 0;

uint8_t currentAttr   = 0;       // 0=occ,1=noise,2=temp,3=light
int     currentLEDCount = 0;
int     targetLEDCount  = 0;

// --------------------------------------------------
// TFT COLORS
// --------------------------------------------------
uint16_t FACE1, FACE2, FACE3;
uint16_t EYE, MOUTH, CHEEK;
uint16_t WARM1, WARM2;
uint16_t COLD1;
uint16_t BG;

// --------------------------------------------------
// HELPER: room ID → index
// --------------------------------------------------
int roomIndexFromId(const String& id) {
  for (int i = 0; i < ROOM_COUNT; i++) {
    if (id == ROOM_IDS[i]) return i;
  }
  return -1;
}

// --------------------------------------------------
// WIFI CONNECT  (multi-SSID: CE-Hub-Student, then Gilang)
// --------------------------------------------------
void connectWiFi() {
  Serial.println("Connecting to WiFi...");

  for (int i = 0; i < numNetworks; i++) {
    Serial.print("Trying SSID: ");
    Serial.println(ssids[i]);

    WiFi.begin(ssids[i], passwords[i]);
    unsigned long startAttempt = millis();

    // Try for ~8 seconds per network
    while (WiFi.status() != WL_CONNECTED &&
           millis() - startAttempt < 8000) {
      Serial.print(".");
      delay(400);
    }

    if (WiFi.status() == WL_CONNECTED) {
      Serial.println("\nConnected!");
      Serial.print("SSID: ");
      Serial.println(ssids[i]);
      Serial.print("IP Address: ");
      Serial.println(WiFi.localIP());
      return;  // success
    }

    Serial.println("\nFailed. Trying next network...");
  }

  // If all networks fail, retry forever
  Serial.println("All WiFi attempts failed. Retrying...");
  delay(2000);
  connectWiFi();
}

// --------------------------------------------------
// NEOPIXEL HELPERS
// --------------------------------------------------
void clearStrip() {
  for (int i = 0; i < LED_COUNT; i++) {
    strip.setPixelColor(i, 0);
  }
  strip.show();
}

// --------------------------------------------------
// TIMELINE RENDERING (Bookings mode)
// --------------------------------------------------
void renderTimeline(int idx) {
  clearStrip();

  RoomData& rd = rooms[idx];
  if (!rd.hasTimeline) {
    Serial.println("renderTimeline: no timeline for this room yet");
    return;
  }

  int N = rd.timelineLen;
  if (N > LED_COUNT) N = LED_COUNT;

  Serial.print("renderTimeline: room ");
  Serial.print(ROOM_IDS[idx]);
  Serial.print(" slots=");
  Serial.println(N);

  for (int i = 0; i < N; i++) {
    uint32_t c = rd.slotBooked[i]
                 ? strip.Color(255, 0, 0, 0)   // red = booked
                 : strip.Color(0, 255, 0, 0);  // green = free

    strip.setPixelColor(i, c);
    strip.show();
    delay(60);
  }
}

// --------------------------------------------------
// STATUS MODE ANIMATION ENGINE
// --------------------------------------------------
uint32_t attrColor(int attr) {
  switch (attr) {
    case 0:
      // Occupancy → deep blue
      return strip.Color(0, 20, 255, 0);

    case 1:
      // Noise → warm yellow
      return strip.Color(255, 255, 0, 0);

    case 2:
      // Temperature → warm-ish
      return strip.Color(0, 255, 0, 20);

    case 3:
      // Light → soft white
      return strip.Color(0, 0, 0, 80);
  }
  return strip.Color(0, 0, 0, 0);
}

void computeAttrTarget(const RoomData& rd, int attr) {
  switch (attr) {
    case 0: targetLEDCount = rd.occupancy / 4.2;          break; // 0–100 → 0–24
    case 1: targetLEDCount = (rd.noise - 30) / 2.1;       break; // 30–80
    case 2: targetLEDCount = (rd.temperature - 17) * 2;   break; // 17–29
    case 3: targetLEDCount = (rd.light - 100) / 21.0;     break; // 100–600
  }
  targetLEDCount   = constrain(targetLEDCount, 0, LED_COUNT);
  currentLEDCount  = 0;
}

void updateStatusAnimation() {
  RoomData& rd = rooms[selectedRoom];
  if (!rd.hasStatus) return;

  unsigned long now = millis();

  // Rotate attribute every 5s
  if (now - lastAttrChange > 5000) {
    lastAttrChange = now;
    currentAttr = (currentAttr + 1) % 4;
    computeAttrTarget(rd, currentAttr);
    clearStrip();
  }

  // Progressive fill
  if (now - lastAnimStep > 120) {
    lastAnimStep = now;
    if (currentLEDCount < targetLEDCount) {
      strip.setPixelColor(currentLEDCount, attrColor(currentAttr));
      strip.show();
      currentLEDCount++;
    }
  }
}

// --------------------------------------------------
// COLOR SETUP
// --------------------------------------------------
void setupColors() {
  BG = ST7735_BLACK;

  FACE1 = tft.color565(255, 170, 0);
  FACE2 = tft.color565(255, 200, 0);
  FACE3 = tft.color565(255, 230, 100);

  EYE   = tft.color565(50, 30, 10);
  MOUTH = tft.color565(200, 0, 0);
  CHEEK = tft.color565(255, 120, 120);

  WARM1 = tft.color565(255, 80, 20);
  WARM2 = tft.color565(255, 140, 60);

  COLD1 = tft.color565(150, 200, 255);
}

// -----------------------------------------------------------
// TFT HEADER
// -----------------------------------------------------------
void drawHeader(int roomIndex) {
  tft.fillRect(0, 0, 128, 20, ST7735_BLUE);

  tft.setTextSize(1);
  tft.setTextColor(ST7735_WHITE);
  tft.setCursor(4, 5);

  if (timelineMode) {
    tft.print("Bookings | ");
  } else {
    tft.print("Condition | ");
  }

  tft.print(ROOM_NAMES[roomIndex]);
}

// -----------------------------------------------------------
// CAPTION (Condition mode only)
// -----------------------------------------------------------
void caption(const char* s) {
  uint16_t shade = tft.color565(40, 40, 40);
  tft.fillRect(0, 120, 128, 20, shade);

  tft.setTextColor(ST7735_WHITE);
  tft.setTextSize(1);

  int16_t x1, y1;
  uint16_t w, h;
  tft.getTextBounds((char*)s, 0, 0, &x1, &y1, &w, &h);
  int x = (128 - (int)w) / 2;

  tft.setCursor(x, 128);
  tft.print(s);
}

// -----------------------------------------------------------
// FACE DRAWING HELPERS
// -----------------------------------------------------------
void clearIcon() {
  // Clear main emoji area (20–120)
  tft.fillRect(0, 20, 128, 100, BG);
}

void drawFaceBase() {
  clearIcon();
  tft.fillCircle(CX, CY, 42, FACE1);
  tft.fillCircle(CX, CY, 38, FACE2);
  tft.fillCircle(CX, CY, 32, FACE3);
}

void eyeHappySoft(int x, int y) {
  tft.drawLine(x - 6, y,   x,     y - 4, EYE);
  tft.drawLine(x,     y - 4, x + 6, y,   EYE);
}

void eyeBusyLeft(int x, int y) {
  tft.drawLine(x - 6, y - 2, x + 2, y + 1, EYE);
}

void eyeBusyRight(int x, int y) {
  tft.drawLine(x - 2, y + 1, x + 6, y - 2, EYE);
}

void eyeSleepy(int x, int y) {
  tft.drawLine(x - 6, y, x + 6, y + 2, EYE);
}

void eyebrowAngry(int x, int y) {
  tft.drawLine(x - 8, y - 8, x + 2,  y - 12, EYE);
  tft.drawLine(x + 2, y - 12, x + 12, y - 8,  EYE);
}

void mouthZigZag() {
  tft.drawLine(CX - 14, CY + 18, CX - 7,  CY + 14, MOUTH);
  tft.drawLine(CX - 7,  CY + 14, CX,     CY + 18, MOUTH);
  tft.drawLine(CX,      CY + 18, CX + 7, CY + 14, MOUTH);
  tft.drawLine(CX + 7,  CY + 14, CX + 14, CY + 18, MOUTH);
}

void mouthChatter() {
  tft.fillRect(CX - 14, CY + 12, 28, 8, ST7735_WHITE);
  for (int i = 0; i < 6; i++) {
    tft.drawLine(CX - 14 + i * 5, CY + 12,
                 CX - 14 + i * 5, CY + 20,
                 EYE);
  }
}

// -----------------------------------------------------------
// EMOTICONS WITH CAPTION (Condition mode)
// -----------------------------------------------------------
void iconPerfect() {
  drawFaceBase();
  tft.fillRoundRect(CX - 25, CY - 12, 16, 12, 4, EYE);
  tft.fillRoundRect(CX + 9,  CY - 12, 16, 12, 4, EYE);
  tft.fillCircle(CX, CY + 18, 16, MOUTH);
  tft.fillCircle(CX, CY + 10, 16, FACE3);
  tft.fillCircle(CX - 22, CY + 10, 6, CHEEK);
  tft.fillCircle(CX + 22, CY + 10, 6, CHEEK);
  caption("Perfect");
}

void iconGood() {
  drawFaceBase();
  eyeHappySoft(CX - 20, CY - 8);
  eyeHappySoft(CX + 20, CY - 8);
  tft.drawLine(CX - 16, CY + 16, CX + 16, CY + 14, MOUTH);
  tft.drawLine(CX - 16, CY + 17, CX + 16, CY + 15, MOUTH);
  tft.fillCircle(CX - 22, CY + 12, 6, CHEEK);
  tft.fillCircle(CX + 22, CY + 12, 6, CHEEK);
  caption("Good");
}

void iconCalm() {
  drawFaceBase();
  tft.drawLine(CX - 26, CY - 4, CX - 14, CY - 6, EYE);
  tft.drawLine(CX + 14, CY - 6, CX + 26, CY - 4, EYE);
  tft.drawLine(CX - 10, CY + 17, CX,     CY + 19, MOUTH);
  tft.drawLine(CX,      CY + 19, CX + 10, CY + 17, MOUTH);
  tft.fillCircle(CX - 22, CY + 10, 5, CHEEK);
  tft.fillCircle(CX + 22, CY + 10, 5, CHEEK);
  caption("Calm");
}

void iconNeutral() {
  drawFaceBase();
  tft.fillRoundRect(CX - 26, CY - 12, 16, 10, 3, EYE);
  tft.fillRoundRect(CX + 10, CY - 12, 16, 10, 3, EYE);
  tft.fillRect(CX - 14, CY + 16, 28, 3, EYE);
  caption("Neutral");
}

void iconBusy() {
  drawFaceBase();
  eyeBusyLeft(CX - 20, CY - 10);
  eyeBusyRight(CX + 20, CY - 10);
  tft.fillCircle(CX, CY + 18, 6, MOUTH);
  tft.fillCircle(CX + 28, CY + 15, 6, ST7735_WHITE);
  tft.fillCircle(CX + 34, CY + 15, 4, ST7735_WHITE);
  tft.fillCircle(CX + 38, CY + 15, 3, ST7735_WHITE);
  caption("Busy");
}

void iconNoisy() {
  drawFaceBase();
  eyebrowAngry(CX - 20, CY - 10);
  eyebrowAngry(CX + 20, CY - 10);
  tft.drawLine(CX - 26, CY - 4, CX - 14, CY + 2, EYE);
  tft.drawLine(CX - 26, CY + 2, CX - 14, CY - 4, EYE);
  tft.drawLine(CX + 14, CY - 4, CX + 26, CY + 2, EYE);
  tft.drawLine(CX + 14, CY + 2, CX + 26, CY - 4, EYE);
  mouthZigZag();
  tft.drawLine(10,  CY - 5,  20,  CY - 10, EYE);
  tft.drawLine(108, CY - 5, 118, CY - 10, EYE);
  caption("Noisy");
}

void iconWarm() {
  drawFaceBase();
  tft.drawLine(CX - 26, CY - 4, CX - 14, CY - 2, EYE);
  tft.drawLine(CX + 14, CY - 2, CX + 26, CY - 4, EYE);
  tft.drawLine(CX - 30, CY - 25, CX - 20, CY - 20, WARM1);
  tft.drawLine(CX - 20, CY - 20, CX - 30, CY - 15, WARM1);
  tft.drawLine(CX + 30, CY - 25, CX + 20, CY - 20, WARM1);
  tft.drawLine(CX + 20, CY - 20, CX + 30, CY - 15, WARM1);
  tft.fillCircle(CX - 26, CY + 4, 4, WARM2);
  tft.fillCircle(CX + 26, CY + 4, 4, WARM2);
  tft.fillCircle(CX, CY + 20, 10, MOUTH);
  tft.fillCircle(CX, CY + 18, 10, FACE3);
  caption("Warm");
}

void iconCold() {
  drawFaceBase();
  tft.drawLine(CX - 28, CY - 16, CX - 12, CY - 22, EYE);
  tft.drawLine(CX + 12, CY - 22, CX + 28, CY - 16, EYE);
  tft.drawLine(CX - 22, CY - 6, CX - 14, CY - 4, EYE);
  tft.drawLine(CX + 14, CY - 4, CX + 22, CY - 6, EYE);
  mouthChatter();
  tft.fillCircle(CX - 30, CY + 4, 3, COLD1);
  tft.fillCircle(CX + 30, CY + 4, 3, COLD1);
  caption("Cold");
}

void iconDim() {
  drawFaceBase();
  eyeSleepy(CX - 20, CY - 6);
  eyeSleepy(CX + 20, CY - 6);
  tft.drawLine(CX - 12, CY + 18, CX + 12, CY + 20, MOUTH);
  tft.setTextColor(ST7735_WHITE);
  tft.setTextSize(1);
  tft.setCursor(CX + 20, CY - 25);
  tft.print("Z");
  caption("Dim");
}

void iconOverloaded() {
  drawFaceBase();
  tft.drawCircle(CX - 20, CY - 10, 8, EYE);
  tft.drawCircle(CX + 20, CY - 10, 8, EYE);
  tft.drawCircle(CX - 20, CY - 10, 4, EYE);
  tft.drawCircle(CX + 20, CY - 10, 4, EYE);
  mouthZigZag();
  tft.drawLine(10,  CY - 15, 25,  CY - 25, MOUTH);
  tft.drawLine(118, CY - 15, 103, CY - 25, MOUTH);
  caption("Overloaded");
}

// -----------------------------------------------------------
// STATE → ICON SELECTOR
// -----------------------------------------------------------
void drawStateIcon(const String& state) {
  if      (state == "perfect")     iconPerfect();
  else if (state == "good")        iconGood();
  else if (state == "calm")        iconCalm();
  else if (state == "busy")        iconBusy();
  else if (state == "noisy")       iconNoisy();
  else if (state == "warm")        iconWarm();
  else if (state == "cold")        iconCold();
  else if (state == "dim")         iconDim();
  else if (state == "overloaded")  iconOverloaded();
  else                             iconNeutral();
}

// -----------------------------------------------------------
// ROOM DETAILS (Bookings Mode) — NO CAPTION
// -----------------------------------------------------------
void showRoomDetails(int idx) {
  // Clear main area
  tft.fillRect(0, 20, 128, 100, BG);
  drawHeader(idx);

  // Ensure caption band is blank (no Condition text bleed)
  tft.fillRect(0, 120, 128, 20, BG);

  tft.setTextWrap(true);
  tft.setTextSize(1);
  tft.setTextColor(ST7735_WHITE);
  tft.setCursor(2, 24);
  tft.println(ROOM_DETAILS[idx]);
}

// -----------------------------------------------------------
// MODE TOGGLE (Bookings ↔ Condition)
// -----------------------------------------------------------
void toggleMode() {
  timelineMode = !timelineMode;

  Serial.print("MODE = ");
  Serial.println(timelineMode ? "BOOKINGS" : "CONDITION");

  if (timelineMode) {
    // BOOKINGS MODE
    showRoomDetails(selectedRoom);
    if (rooms[selectedRoom].hasTimeline) {
      renderTimeline(selectedRoom);
    } else {
      clearStrip();
    }
  } else {
    // CONDITION MODE
    drawHeader(selectedRoom);
    if (rooms[selectedRoom].hasStatus) {
      drawStateIcon(rooms[selectedRoom].state);
      computeAttrTarget(rooms[selectedRoom], currentAttr);
    } else {
      iconNeutral();
    }
    clearStrip();
  }
}

// -----------------------------------------------------------
// MQTT CALLBACK — HANDLE TIMELINE + CONDITION DATA
// -----------------------------------------------------------
void mqttCallback(char* topic, byte* payload, unsigned int length) {
  Serial.print("TOPIC: ");
  Serial.println(topic);

  String json;
  for (unsigned int i = 0; i < length; i++) {
    json += (char)payload[i];
  }

  Serial.print("RAW MQTT PAYLOAD: ");
  Serial.println(json);

  StaticJsonDocument<1024> doc;
  auto err = deserializeJson(doc, json);
  if (err) {
    Serial.print("JSON parse error: ");
    Serial.println(err.c_str());
    return;
  }

  bool isTimeline = doc.containsKey("timeline");
  bool isStatus   = doc.containsKey("state");

  Serial.print("isTimeline=");
  Serial.print(isTimeline);
  Serial.print(" isStatus=");
  Serial.println(isStatus);

  // Determine room id
  String rid;
  if (doc.containsKey("room_id")) {
    rid = doc["room_id"].as<const char*>();
  } else if (doc.containsKey("room")) {
    rid = doc["room"].as<const char*>();
  } else {
    String t = topic;
    int pos = t.lastIndexOf('/');
    if (pos >= 0) rid = t.substring(pos + 1);
  }

  Serial.print("Resolved room id: ");
  Serial.println(rid);

  int idx = roomIndexFromId(rid);
  if (idx < 0) {
    Serial.print("Unknown room id: ");
    Serial.println(rid);
    return;
  }

  RoomData& rd = rooms[idx];

  // --- TIMELINE / BOOKINGS UPDATE ---
  if (isTimeline) {
    JsonArray arr = doc["timeline"].as<JsonArray>();
    rd.timelineLen = arr.size();
    if (rd.timelineLen > LED_COUNT) rd.timelineLen = LED_COUNT;

    for (int i = 0; i < rd.timelineLen; i++) {
      const char* v = arr[i];
      rd.slotBooked[i] = (strcmp(v, "booked") == 0);
      Serial.print("timeline[");
      Serial.print(i);
      Serial.print("]=");
      Serial.println(v);
    }

    rd.hasTimeline = true;
    Serial.print("Bookings update for room ");
    Serial.print(ROOM_IDS[idx]);
    Serial.print("  slots=");
    Serial.println(rd.timelineLen);

    if (idx == selectedRoom && timelineMode) {
      showRoomDetails(idx);
      renderTimeline(idx);
    }
  }

  // --- CONDITION UPDATE ---
  if (isStatus) {
    rd.occupancy   = doc["occupancy"]   | 0.0;
    rd.noise       = doc["noise"]       | 0.0;
    rd.temperature = doc["temperature"] | 0.0;
    rd.light       = doc["light"]       | 0.0;
    rd.state       = doc["state"].as<String>();
    rd.hasStatus   = true;

    Serial.print("Condition update for room ");
    Serial.print(ROOM_IDS[idx]);
    Serial.print("  state=");
    Serial.println(rd.state);

    if (idx == selectedRoom && !timelineMode) {
      drawHeader(selectedRoom);
      drawStateIcon(rd.state);
      computeAttrTarget(rooms[selectedRoom], currentAttr);
      clearStrip();
    }
  }
}

// -----------------------------------------------------------
// MQTT CONNECT
// -----------------------------------------------------------
void connectMQTT() {
  mqttClient.setServer(MQTT_HOST, MQTT_PORT);
  mqttClient.setCallback(mqttCallback);

  // IMPORTANT: allow large JSON (timeline)
  mqttClient.setBufferSize(512);

  while (!mqttClient.connected()) {
    Serial.print("Connecting to MQTT...");
    String clientId = "UCLDevice-";
    clientId += String(random(0xFFFF), HEX);

    if (mqttClient.connect(clientId.c_str(), MQTT_USER, MQTT_PASS)) {
      Serial.println("connected!");

      // Timeline (bookings) feed for all rooms
      mqttClient.subscribe("student/CASA0019/Gilang/studyspace/+/timeline");
      // Status (condition) feed for all rooms
      mqttClient.subscribe("student/CASA0019/Gilang/studyspace/+/status");

      Serial.println("Subscribed → student/CASA0019/Gilang/studyspace/+/timeline");
      Serial.println("Subscribed → student/CASA0019/Gilang/studyspace/+/status");

    } else {
      Serial.print("failed, rc=");
      Serial.print(mqttClient.state());
      Serial.println(" retrying...");
      delay(2000);
    }
  }
}

// -----------------------------------------------------------
// SETUP
// -----------------------------------------------------------
void setup() {
  Serial.begin(115200);
  delay(1500);
  Serial.println("=== UCL Study Space Visualiser v2 (timeline fix) ===");

  // Rotary encoder
  pinMode(ENC_CLK, INPUT_PULLUP);
  pinMode(ENC_DT,  INPUT_PULLUP);
  pinMode(ENC_SW,  INPUT_PULLUP);
  lastClk = digitalRead(ENC_CLK);

  // TFT
  tft.initR(INITR_BLACKTAB);
  tft.setRotation(0);
  setupColors();
  tft.fillScreen(BG);

  // NeoPixel
  strip.begin();
  strip.setBrightness(40);
  clearStrip();

  // WiFi + MQTT
  connectWiFi();
  connectMQTT();

  // Initial screen (Bookings mode)
  showRoomDetails(selectedRoom);
}

// -----------------------------------------------------------
// LOOP
// -----------------------------------------------------------
void loop() {
  // MQTT keep-alive
  if (!mqttClient.connected()) {
    connectMQTT();
  }
  mqttClient.loop();

  // --- ROTARY ENCODER ROTATION ---
  int currentClk = digitalRead(ENC_CLK);
  if (currentClk != lastClk) {
    if (currentClk == LOW) {
      // Direction
      if (digitalRead(ENC_DT) != currentClk) {
        selectedRoom = (selectedRoom + 1) % ROOM_COUNT;
      } else {
        selectedRoom--;
        if (selectedRoom < 0) selectedRoom = ROOM_COUNT - 1;
      }

      Serial.print("Room → ");
      Serial.print(ROOM_IDS[selectedRoom]);
      Serial.print(" (");
      Serial.print(ROOM_NAMES[selectedRoom]);
      Serial.println(")");

      if (timelineMode) {
        showRoomDetails(selectedRoom);
        if (rooms[selectedRoom].hasTimeline) {
          renderTimeline(selectedRoom);
        } else {
          clearStrip();
        }
      } else {
        drawHeader(selectedRoom);
        if (rooms[selectedRoom].hasStatus) {
          drawStateIcon(rooms[selectedRoom].state);
          computeAttrTarget(rooms[selectedRoom], currentAttr);
        } else {
          iconNeutral();
        }
        clearStrip();
      }
    }
    lastClk = currentClk;
  }

  // --- ROTARY BUTTON (MODE SWITCH) ---
  bool b = digitalRead(ENC_SW);
  if (b != lastButton && (millis() - lastButtonTime > BUTTON_DEBOUNCE)) {
    lastButtonTime = millis();
    lastButton = b;
    if (b == LOW) {
      toggleMode();
    }
  }

  // --- CONDITION MODE ANIMATION ---
  if (!timelineMode) {
    updateStatusAnimation();
  }

  delay(5);
}
