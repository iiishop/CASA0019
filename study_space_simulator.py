import requests
import json
import time
import random
from datetime import datetime, timedelta
import paho.mqtt.client as mqtt

# ============================
# CONFIG — CHANGE IF NEEDED
# ============================
TOKEN = "uclapi-0ba9562db44f415-1028a0691df6053-a02437e11cd34a2-d47078ea7a18397"
LOCATION_ID = 3438
DATE = "2025-12-05"
API_URL = "https://uclapi.com/libcal/space/bookings"

ROOMS = ["24380", "24381", "24382", "24546", "24547"]

T_START = datetime.strptime(DATE + " 09:00", "%Y-%m-%d %H:%M")
T_END   = datetime.strptime(DATE + " 21:00", "%Y-%m-%d %H:%M")
SLOT_MINUTES = 30

BROKER_HOST = "mqtt.cetools.org"
BROKER_PORT = 1884
MQTT_USERNAME = "student"
MQTT_PASSWORD = "ce2021-mqtt-forget-whale"
MQTT_BASE = "student/CASA0019/Gilang/studyspace"

UPDATE_INTERVAL = 60   # seconds

# ============================
# HELPERS
# ============================
def overlaps(slot_start, slot_end, book_start, book_end):
    return not (slot_end <= book_start or slot_start >= book_end)

def classify_room(occ, noise, temp, light):

    if occ >= 90 and noise >= 60:
        return "overloaded"

    if temp > 27.5:
        return "warm"

    if temp < 18.5:
        return "cold"

    if light < 240:
        return "dim"

    if noise >= 60 and occ < 90:
        return "noisy"

    if 70 <= occ <= 90 and noise < 60:
        return "busy"

    if occ < 30 and noise < 40 and 21 <= temp <= 24 and light >= 360:
        return "perfect"

    if occ < 50 and noise < 45 and 20 <= temp <= 26:
        return "good"

    if noise < 44 and occ < 60 and 19 <= temp <= 26:
        return "calm"

    return "neutral"

# ============================
# SIMULATED ENVIRONMENT DATA
# ============================
def simulate_values_for_room(room):

    ts = datetime.now()
    hour = ts.hour

    if 7 <= hour <= 10:
        base_occ = 35
    elif 11 <= hour <= 16:
        base_occ = 70
    elif 17 <= hour <= 21:
        base_occ = 55
    else:
        base_occ = 20

    room_occ_bias = {
        "24546": +10,
        "24547": +0,
        "24380": -5,
        "24381": +15,
        "24382": -10
    }.get(room, 0)

    occ = random.gauss(base_occ + room_occ_bias, 18)
    if random.random() < 0.10:
        occ += random.randint(20, 40)
    occ = max(0, min(100, occ))

    noise = 28 + occ * 0.45 + random.gauss(0, 5)
    if random.random() < 0.15:
        noise += random.randint(10, 25)
    noise = max(30, min(85, noise))

    base_temp = 22.5 + (0.7 if 12 <= hour <= 17 else 0)
    room_temp_bias = {
        "24546": +0.3,
        "24547":  0,
        "24380": -0.5,
        "24381": +1.0,
        "24382": -1.0
    }.get(room, 0)

    temp = random.gauss(base_temp + room_temp_bias, 1.0)
    if random.random() < 0.05:
        temp += random.uniform(2, 4)
    if random.random() < 0.05:
        temp -= random.uniform(2, 4)
    temp = max(17, min(29, temp))

    base_light = 380
    light = random.gauss(base_light, 35)
    if random.random() < 0.07:
        light = random.gauss(180, 40)
    if random.random() < 0.05:
        light = random.gauss(480, 40)
    light = max(100, min(600, light))

    state = classify_room(occ, noise, temp, light)

    return {
        "timestamp": ts.isoformat(timespec="seconds"),
        "room": room,
        "occupancy": round(occ, 1),
        "noise": round(noise, 1),
        "temperature": round(temp, 1),
        "light": round(light, 1),
        "state": state
    }

# ============================
# TIMELINE BUILDER
# ============================
def build_timeline(bookings):
    timeline = []
    current = T_START

    while current < T_END:
        slot_start = current
        slot_end = current + timedelta(minutes=SLOT_MINUTES)

        booked = False
        for b in bookings:
            fs = datetime.fromisoformat(b["from_date"]).replace(tzinfo=None)
            ts = datetime.fromisoformat(b["to_date"]).replace(tzinfo=None)

            if overlaps(slot_start, slot_end, fs, ts):
                booked = True
                break

        timeline.append("booked" if booked else "free")
        current = slot_end

    return timeline

# ============================
# FETCH LIBCAL BOOKINGS
# ============================
def fetch_bookings():
    params = {
        "token": TOKEN,
        "lid": LOCATION_ID,
        "date": DATE
    }

    r = requests.get(API_URL, params=params)
    data = r.json()

    if not data["ok"]:
        print("UCL API ERROR:", data)
        return {}

    all_bookings = data["bookings"]

    grouped = {room_id: [] for room_id in ROOMS}

    for b in all_bookings:
        eid = str(b["eid"])
        if eid in grouped:
            grouped[eid].append(b)

    return grouped

# ============================
# MAIN LOOP
# ============================
def main():

    print("Connecting to MQTT broker...")
    client = mqtt.Client()
    client.username_pw_set(MQTT_USERNAME, MQTT_PASSWORD)
    client.connect(BROKER_HOST, BROKER_PORT, keepalive=60)
    client.loop_start()

    print("Fetching LibCal + Simulating environment data...")

    while True:
        grouped_bookings = fetch_bookings()

        ts = datetime.now().isoformat(timespec="seconds")
        print(f"\n===== UPDATE {ts} =====")

        for room in ROOMS:

            # ---- Timeline ----
            bookings = grouped_bookings.get(room, [])
            timeline = build_timeline(bookings)

            client.publish(
                f"{MQTT_BASE}/{room}/timeline",
                json.dumps({"room": room, "timeline": timeline})
            )

            # ---- Environment Status ----
            msg = simulate_values_for_room(room)
            client.publish(
                f"{MQTT_BASE}/{room}/status",
                json.dumps(msg)
            )

            print(room, "timeline:", timeline)
            print(room, "status:", msg)

        time.sleep(UPDATE_INTERVAL)

# ============================
# RUN
# ============================
if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\nStopped by user.")
