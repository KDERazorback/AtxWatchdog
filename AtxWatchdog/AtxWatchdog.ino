/**
 * AtxWatchdog
 * Allow for managing and automated diagnosis of PSU Power Supplies
 * 
 * @author Fabian Ramos R (KDERazorback)
 * @version 1.0
 */

/*
 * Calibration curves: Syntax: <calCurve2/calCurve3/calCurve4>  x^4 x^3 x^2 x^1 o
 *  V12: calCurve4 0.0000000005904564646f, -0.000001065254196f, 0.0007292712878f, -0.2083356261f, 25.39229605f
 *  V5: calCurve3 0.00000003093601f, -0.000050162482454f, 0.033847756713564f, -4.93920991120435f
 *  V5SB: calCurve3 0.000000144284615f, -0.000188086682585f, 0.089558333834226f, -12.314665470356156f
 *  V3.3: calCurve4 0.000000000158821f, -0.000000279452017f, 0.000186315759286f, -0.050800891265817f, 6.1451224275267f
 *  
 *  New calibration curves:
 *  V5: calCurve2 0.3068071f, 0.1962099f, 1.100424f
 */

#include "PiezoBuzzer.h"
#include "AtxVoltmeter.h"
#include "AtxWatchdogDFU.h"

// LEFT SIDE
#define MODE_SEL 4
#define PSU_OK 5
#define TONE 6 // 6=Buzzer, 13=Onboard SMD LED
#define PS_ON_SENSE 7
#define PWR_OK 8
#define PS_ON_TRIGGER 9

// Data headers
#define DATA_PACKET_START 0x11
#define DATA_MARKER_METADATA_START 0x13

// RIGHT SIDE
#define V12_SENSE A3
#define V5_SENSE A2
#define V5SB_SENSE A1
#define V3_3_SENSE A0

// Prescaler bit manipulation macros
#define cbi(sfr, bit) (_SFR_BYTE(sfr) &= ~_BV(bit))
#define sbi(sfr, bit) (_SFR_BYTE(sfr) |= _BV(bit))

// THRESHOLD VALUES
#define ON_OFF_RAIL_SUM_THRESHOLD 0.8f // Defines the value where the PSU is considered ON when adding all primary rail values
#define T1_MAX_TIMEFRAME 500  // Maximum amount of time this state can last before declaring a failure (in ms)
#define T2_MAX_TIMEFRAME 1000 // Maximum amount of time this state can last before declaring a failure (in ms)
#define T3_MAX_TIMEFRAME 5000 // Maximum amount of time this state can last before declaring a failure (in ms)
#define T6_MAX_TIMEFRAME 5000 // Maximum amount of time this state can last before declaring a failure (in ms).
// This value describes the duration of the T6 state recording when in PSU TEST MODE

// Available session marks that can be stored inside sessionPacketMarks array (see below). These values are indexes of the mark array
#define SESSION_MARK_T1 0x00
#define SESSION_MARK_T2 0x01
#define SESSION_MARK_T3 0x02
#define SESSION_MARK_ON 0x03
#define SESSION_MARK_T6 0x04
#define SESSION_MARK_POFF 0x05
#define SESSION_TOTAL_MARKS 6

// Mode and tacking fields
bool PSU_OK_ACTIVE = false; // ActiveHigh
bool BOARD_PSU_MODE = false; // ActiveHigh

// Session fields (for use with sessionStart() and sessionEnd()
unsigned long lastUpdateMicros = 0;
unsigned long serialDataPacketsSent = 0;
unsigned long sessionStartMicros = 0;
unsigned long sessionPacketMarks[SESSION_TOTAL_MARKS]; // Used to record the location of various PSU events. This records packet indexes: T1 start, T2 start, T3 start, PON start, T6 start, POFF start
unsigned long sessionTimeMarks[SESSION_TOTAL_MARKS]; // Used to record the location in time of various PSU events. This records microseconds relative to sessionStartMicros.

// Classes
PiezoBuzzer buzzer(TONE);
AtxVoltmeter Atx(V12_SENSE, V5_SENSE, V5SB_SENSE, V3_3_SENSE, PS_ON_SENSE, PS_ON_TRIGGER, PWR_OK);

// Sketch setup code
void setup() {
  Serial.begin(2000000);
  Serial.println("ATX_WATCHDOG");
  Serial.println("Booting...");

  // Set ADC prescaler to 16
  sbi(ADCSRA, ADPS2); // Set b2
  cbi(ADCSRA, ADPS1); // Clear b1
  cbi(ADCSRA, ADPS0); // Clear b0

  // Setup Pins
  pinMode(MODE_SEL, INPUT);
  pinMode(PSU_OK, OUTPUT);
  digitalWrite(PSU_OK, LOW);
  pinMode(TONE, OUTPUT);
  pinMode(PS_ON_TRIGGER, OUTPUT);

  pinMode(PS_ON_SENSE, INPUT);
  pinMode(PWR_OK, INPUT);

  // Read Bord mode from jumper selector
  BOARD_PSU_MODE = digitalRead(MODE_SEL);

  Serial.print("BOARD_MODE: ");
  if (BOARD_PSU_MODE)
    Serial.println("PSU ONLY");
  else
    Serial.println("PC BRIDGE");

  bool enterDfu = dfuCheck(1500); // Check the incoming serial stream for 1.5 seconds and enter DFU mode if signaled
  // In DFU mode the device can be reconfigured, or put on hold for a certain amount of time

  if (enterDfu) dfuMode();

  // Set ATX Calibration
  Atx.setV12Coefficients  (0.0000000005904564646f, -0.000001065254196f, 0.0007292712878f   , -0.2083356261f     , 25.39229605f        );
  Atx.setV5Coefficients   (0                     , 0                  , 0.3315f            , 0.0330f            , 1.3702f             );
  Atx.setV5sbCoefficients (0                     , 0.000000144284615f , -0.000188086682585f, 0.089558333834226f , -12.314665470356156f);
  Atx.setV3_3Coefficients (0.000000000158821f    , -0.000000279452017f, 0.000186315759286f , -0.050800891265817f, 6.1451224275267f    );

  Atx.setSamplingAvgCount(2);

  // Signal BOOT complete
  Serial.println("Boot OK");
  buzzer.beep();
}

void loop() {
  while (buzzer.isBeeping())
    buzzer.update(); // Ensure the TONE signal is turned off properly

  if (BOARD_PSU_MODE)
  {
    loop_psumode();
  }
  else
  {
    loop_livemode();
  }
}

void sendSerialInt(int value)
{
  byte b = ((value & 0xff00) >> 8);
  Serial.write(b);

  b = (value & 0xff);
  Serial.write(b);
}

void sendSerialLong(long value)
{
  int i = ((value & 0xffff0000) >> 16);
  sendSerialInt(i);

  i = (value & 0xffff);
  sendSerialInt(i);
}

void sessionStart()
{
  for (int i = 0; i < SESSION_TOTAL_MARKS; i++)
  {
    sessionTimeMarks[i] = 0;
    sessionPacketMarks[i] = 0;
  }
  
  Serial.println("[SESS_START]");
  sessionStartMicros = micros();
  serialDataPacketsSent = 0;
}

void sessionRecordMark(int index) {
  sessionPacketMarks[index] = serialDataPacketsSent;
  sessionTimeMarks[index] = micros() - sessionStartMicros;
}

void sessionEnd(bool success) {
  unsigned long endMicros = micros();

  unsigned long t1_t = sessionTimeMarks[1] - sessionTimeMarks[0]; // T2 - T1
  unsigned long t2_t = sessionTimeMarks[2] - sessionTimeMarks[1]; // T3 - T2
  unsigned long t3_t = sessionTimeMarks[3] - sessionTimeMarks[2]; // PON - T3
  unsigned long on_t = sessionTimeMarks[4] - sessionTimeMarks[3]; // T6 - PON
  unsigned long off_t = sessionTimeMarks[5] - sessionTimeMarks[4]; // POFF - T6

  delay(350); // If we do not stop for a second, the serial port appears to overflow in chinese boards
  
  if (success) 
  {   
    Serial.println("Power up sequence completed.");
    Serial.print("T1 passed in ");
    Serial.print(t1_t, DEC);
    Serial.println("us.");
    Serial.print("T2 passed in ");
    Serial.print(t2_t, DEC);
    Serial.println("us.");
    Serial.print("T3 passed in ");
    Serial.print(t3_t, DEC);
    Serial.println("us.");

    Serial.print("Was ON for ");
    Serial.print(on_t, DEC);
    Serial.println("us.");

    Serial.print("T6 passed in ");
    Serial.print(off_t, DEC);
    Serial.println("us.");

    Serial.print("PON reached in ");
    Serial.print(t1_t + t2_t + t3_t, DEC);
    Serial.println("us.");

    // Beep 3 times and signal a good PSU
    SetPsuOk(true);
    Serial.println("PSU is GOOD");
    for (int i = 1; i <= 3; i++)
    {
      buzzer.beep();
      while (buzzer.isBeeping())
        buzzer.update();

      delay(350);
    }
  }
  else {
    Serial.println("Power up sequence failed.");

    if (sessionPacketMarks[0] > 0 && sessionPacketMarks[1] > 0) // T1
    {
      Serial.print("T1 lasted ");
      Serial.print(t1_t, DEC);
      Serial.println("us.");
    }

    if (sessionPacketMarks[2] > 0) // T2
    {
      Serial.print("T2 lasted ");
      Serial.print(t2_t, DEC);
      Serial.println("us.");
    }

    if (sessionPacketMarks[3] > 0) // T3
    {
      Serial.print("T3 lasted ");
      Serial.print(t3_t, DEC);
      Serial.println("us.");
    }

    if (sessionPacketMarks[4] > 0) // ON
    {
      Serial.print("Was ON for ");
      Serial.print(on_t, DEC);
      Serial.println("us.");
    }

    if (sessionPacketMarks[5] > 0) // T6
    {
      Serial.print("T6 passed in ");
      Serial.print(off_t, DEC);
      Serial.println("us.");
    }

    // Beep 1 long time and signal a bad PSU
    SetPsuOk(false);
    Serial.println("PSU is BAD");
    buzzer.beep();
    delay(1200); // 1.2 sec
    while (buzzer.isBeeping())
        buzzer.update();
  }

  // Send statistics packets
  // Send marker packets
  Serial.write(DATA_MARKER_METADATA_START); // begin data
  Serial.write(48); // Metadata length: (4 bytes of sessionPacketMarks + sessionTimeMarks) times sessionPacketMarks.length
  for (int i = 0; i < SESSION_TOTAL_MARKS; i++)
  {
    sendSerialLong(sessionPacketMarks[i]); // 4 bytes
    sendSerialLong(sessionTimeMarks[i]); // 4 bytes
  } // total of 48 data bytes

  Serial.println("[SESS_END]");
}

void updateStatus()
{
  /*
   * Protocol version: 1.2
   * 1 bit preamble (H) + 63 bits data
   */

   Atx.update();

   unsigned long currmicros = micros();
   unsigned long timeoffset = lastUpdateMicros - currmicros;
   lastUpdateMicros = currmicros;

   // Send voltages over serial as Ints trimmed to the specified bit count
   int v12 = Atx.V12() * 1000.0f; // 14 bits: max=16383mv
   int v5 = Atx.V5() * 1000.0f; // 13 bits: max=8191mv
   int v5sb = Atx.V5SB() * 1000.0f; // 13 bits: max=8191mv
   int v3_3 = Atx.V3_3() * 1000.0f; // 13 bits: max=8191mv

   if (v12 & 0xC000) v12 = 0x3FFF;
   if (v5 & 0xE000) v5 = 0x1FFF;
   if (v5sb & 0xE000) v5sb = 0x1FFF;
   if (v3_3 & 0xE000) v3_3 = 0x1FFF;

   /* --- */                                 v12  |= 0b1000000000000000; // Always set
   if (BOARD_PSU_MODE)                       v12  |= 0b0100000000000000; // BOARD_PSU_MODE

   if (timeoffset < 128) // A value greater than 127 will be treated as an overflow on the receiver. Its numeric interpretation is up to it
   {
      if (timeoffset & 0b01000000 > 0)       v5   |= 0b1000000000000000; // Timeoffset b6
      if (timeoffset & 0b00100000 > 0)       v5   |= 0b0100000000000000; // Timeoffset b5
      if (timeoffset & 0b00010000 > 0)       v5   |= 0b0010000000000000; // Timeoffset b4

      if (timeoffset & 0b00001000 > 0)       v5sb |= 0b1000000000000000; // Timeoffset b3
   }
   else
   {
      // Clear 3 MSB from v5
      v5 &= 0b0001111111111111;
      // Clear 1 MSB from v5sb
      v5sb &= 0b0111111111111111;
   }
   
   if (PSU_OK_ACTIVE)                        v5sb |= 0b0100000000000000; // PSU_OK_ACTIVE
   if (Atx.isTriggered())                    v5sb |= 0b0010000000000000; // ATX_IS_TRIGGERED

   if (Atx.isPgGoodPresent())                v3_3 |= 0b1000000000000000; // PWR_OK Present
   if (Atx.isPsOnPresent())                  v3_3 |= 0b0100000000000000; // PS_ON Present
   if (buzzer.isBeeping())                   v3_3 |= 0b0010000000000000; // Buzzer is beeping

   sendSerialInt(v12);
   sendSerialInt(v5);
   sendSerialInt(v5sb);
   sendSerialInt(v3_3);

   serialDataPacketsSent++;
}

void SetPsuOk(bool value)
{
  if (value == PSU_OK_ACTIVE)
    return;
  
  if (value)
    digitalWrite(PSU_OK, HIGH);
  else
    digitalWrite(PSU_OK, LOW);

  PSU_OK_ACTIVE = value;
}

bool IsOutOfSpec(float val, float expected)
{
  return (abs(val - expected) > (expected * 0.05f));
}

void writeSessionOOSFlags(byte oosflags, int maxOosRailData[], bool leaveOpen) {
  if (oosflags == 0)
  {
    if (!leaveOpen)
      sessionEnd(true);
    Serial.print("All rails are stable and within spec. [OOS:0x0]");
  }
  else
  {
    if (!leaveOpen)
      sessionEnd(false);
    Serial.print("PSU is outside specs. OOS error code: [OOS:0x");
    Serial.print(oosflags, HEX);
    Serial.println("]");

    if ((oosflags & 0b00000001) > 0)
    {
        Serial.print("V12 is out of specs!. ");
        Serial.print(maxOosRailData[0], DEC);
        Serial.println("mV");
    }
    if ((oosflags & 0b00000010) > 0)
    {
        Serial.print("V5 is out of specs!. ");
        Serial.print(maxOosRailData[1], DEC);
        Serial.println("mV");
    }
    if ((oosflags & 0b00000100) > 0)
    {
        Serial.print("V5SB is out of specs!. ");
        Serial.print(maxOosRailData[2], DEC);
        Serial.println("mV");
    }
    if ((oosflags & 0b00001000) > 0)
    {
        Serial.print("V3_3 is out of specs!. ");
        Serial.print(maxOosRailData[3], DEC);
        Serial.println("mV");
    }

    if ((oosflags & 0b00010000) > 0)
        Serial.print("PWR_OK disappeared!. ");
  }
}

void loop_psumode() {
    while (buzzer.isBeeping())
    buzzer.update();
    // Board is in PSU Testing Mode
    Serial.println("PSU TEST MODE");
    delay(3000);
    Atx.update();

    bool psuPresentedLate = false;
    
    while (!Atx.isPsuPresent())
    {
        Serial.println("PSU is not present.");
        psuPresentedLate = true;
        delay(2000);
        Atx.update();
    }

    Serial.println("PSU present.");
    if (psuPresentedLate)
    {
      Serial.println("Letting Main caps charge for 10s...");
      delay(10000);
    }

    // POFF
    if (Atx.isV5sbPresent() && !Atx.isOn() && Atx.isPsOnPresent())
      Serial.println("PSU status POFF");
    else
    {
       while (!Atx.isV5sbPresent())
       {
          Serial.print("V5SB is not present. ");
          Serial.print(Atx.V5SB(), DEC);
          Serial.println(" mV");
          delay(2000);
          Atx.update();
       }
       while (!Atx.isPsOnPresent())
       {
          Serial.println("PS_ON is not present.");
          delay(2000);
          Atx.update();
       }
    }

    Serial.println("Turning on...");
    sessionStart(); // From this point forward, try to keep serial-log silence. Try to send data bytes or error conditions only

   unsigned long startMillis = millis();
    while ((millis() - startMillis) < 1000)
      updateStatus(); // Record a little before turning the PSU on    

    bool v5sbOos = false; // Indicates if V5SB falled below spec during this step
    Atx.turnOn();
    sessionRecordMark(SESSION_MARK_T1); // T1: Mark the point where the PSU was turned ON
    startMillis = millis();
    // T1
    while (true)
    {
      if ((millis() - startMillis) > T1_MAX_TIMEFRAME)
      {
          Atx.turnOff();
          sessionEnd(false);

          Serial.println("PSU did not entered T2. Giving up.");
          if (v5sbOos & 0x02 > 0)
            Serial.println("+5VSB disappeared (!).");
          else if (v5sbOos)
            Serial.println("+5VSB falled out of spec (!)."); 
            
          while (true);
      }

      updateStatus();

      if (!Atx.isV5sbPresent() || IsOutOfSpec(Atx.V5SB(), 5.0f))
        v5sbOos = (Atx.isV5sbPresent() ? 0x00 : 0x02) | 0x01;

      if (Atx.V12() + Atx.V5() + Atx.V3_3() > ON_OFF_RAIL_SUM_THRESHOLD)
        break; // Enter T2
    }

    sessionRecordMark(SESSION_MARK_T2); // T2: Mark the point where the PSU entered T2

    if (v5sbOos)
    {
        Atx.turnOff();
        sessionEnd(false);
        
        if (v5sbOos & 0x02 > 0)
          Serial.println("+5VSB disappeared (!).");
        else if (v5sbOos)
          Serial.println("+5VSB falled out of spec (!).");
        
        Serial.println("Failed at T1");
        
        while (true);    
    }
    
    // T2
    bool v12decayed = false;
    bool v5decayed = false;
    bool v3_3decayed = false;

    float lastV12 = 0.0f;
    float lastV5 = 0.0f;
    float lastV3_3 = 0.0f;

    // TODO: Check if PWR_OK is not asserted before all rails are within range

    startMillis = millis();

    while (true)
    {
      if ((millis() - startMillis) > T2_MAX_TIMEFRAME)
      {
          Atx.turnOff();
          sessionEnd(false);

          Serial.println("PSU did not entered T3. Giving up.");
          if (v5sbOos & 0x02 > 0)
            Serial.println("+5VSB disappeared (!).");
          else if (v5sbOos)
            Serial.println("+5VSB falled out of spec (!)."); 
          
          while (true);
      }

      updateStatus();

      if (!Atx.isV5sbPresent() || IsOutOfSpec(Atx.V5SB(), 5.0f))
        v5sbOos = (Atx.isV5sbPresent() ? 0x00 : 0x02) | 0x01;

      if (lastV12 > Atx.V12())
        v12decayed = true;

      if (lastV5 > Atx.V5())
        v5decayed = true;

      if (lastV3_3 > Atx.V3_3())
        v3_3decayed = true;

      lastV12 = Atx.V12();
      lastV5 = Atx.V5();
      lastV3_3 = Atx.V3_3();

      if (!IsOutOfSpec(lastV12, 12.0f) && !IsOutOfSpec(lastV5, 5.0f) && !IsOutOfSpec(lastV3_3, 3.3f))
        break; // Enter T3
    }

    sessionRecordMark(SESSION_MARK_T3); // T3: Mark the point where the PSU entered T3
    
    if (v5sbOos)
    {
        Atx.turnOff();
        sessionEnd(false);

        if (v5sbOos & 0x02 > 0)
          Serial.println("+5VSB disappeared (!).");
        else if (v5sbOos)
          Serial.println("+5VSB falled out of spec (!).");
        
        Serial.println("Failed at T2");
        
        while (true);    
    }

    // T3
    startMillis = millis();

    while (true)
    {
      if ((millis() - startMillis) > T3_MAX_TIMEFRAME)
      {
          Atx.turnOff();
          sessionEnd(false);

          Serial.println("PSU did not entered PON. Giving up.");
          if (v5sbOos & 0x02 > 0)
            Serial.println("+5VSB disappeared (!).");
          else if (v5sbOos)
            Serial.println("+5VSB falled out of spec (!)."); 
            
          while (true);
      }

      updateStatus();

      if (!Atx.isV5sbPresent() || IsOutOfSpec(Atx.V5SB(), 5.0f))
        v5sbOos = (Atx.isV5sbPresent() ? 0x00 : 0x02) | 0x01;

      // TODO: Continue checking main rails for specs

      if (Atx.isPgGoodPresent())
        break;
    }

    sessionRecordMark(SESSION_MARK_ON); // ON: Mark the point where the PSU entered PON

    if (v5sbOos)
    {
        Atx.turnOff();
        sessionEnd(false);

        if (v5sbOos & 0x02 > 0)
          Serial.println("+5VSB disappeared (!).");
        else if (v5sbOos)
          Serial.println("+5VSB falled out of spec (!).");
        
        Serial.println("Failed at T3");
        
        while (true);    
    }

    // Reached PON
    byte oosflags = 0;
    int maxOosRailData[4];
    maxOosRailData[0] = 12.0f;
    maxOosRailData[1] = 5.0f;
    maxOosRailData[2] = 5.0f;
    maxOosRailData[3] = 3.3f;
    startMillis = millis();
    while ((millis() - startMillis) < 20000) // Record PSU activity for 20 seconds.
    // Setting the above timeout value must be done while taking in consideration that if the psu has devices attached to it
    // like mechanical hard disks (which should not be done while testing anyways), the controller should give enough time
    // for them to be able to gain enough rotational speed for a full safe parking procedure. This takes about 15 seconds
    // for fast RPM disks with slow controllers
    {
        // Check if PSU is stable
        updateStatus();
        if (IsOutOfSpec(Atx.V12(), 12.0f))
        {
            if (abs(maxOosRailData[0] - 12.0f) < abs(Atx.V12() - 12.0f)) maxOosRailData[0] = Atx.V12();
            oosflags |= 0b00000001;
        }

        if (IsOutOfSpec(Atx.V5(), 5.0f))
        {
            if (abs(maxOosRailData[1] - 5.0f) < abs(Atx.V5() - 5.0f)) maxOosRailData[1] = Atx.V5();
            oosflags |= 0b00000010;
        }

        if (IsOutOfSpec(Atx.V5SB(), 5.0f))
        {
            if (abs(maxOosRailData[2] - 5.0f) < abs(Atx.V5SB() - 5.0f)) maxOosRailData[2] = Atx.V5SB();
            oosflags |= 0b00000100;
        }

        if (IsOutOfSpec(Atx.V3_3(), 3.3f))
        {
            if (abs(maxOosRailData[3] - 3.3f) < abs(Atx.V3_3() - 3.3f)) maxOosRailData[3] = Atx.V3_3();
            oosflags |= 0b00001000;
        }

        if (!Atx.isPgGoodPresent())
                oosflags |= 0b00010000;
    }

    // POFF
    Atx.turnOff();
    sessionRecordMark(SESSION_MARK_T6); // T6: Mark the point where the PSU was turned off

    // Record turnoff curve
    bool active = true;
    startMillis = millis();
    while ((millis() - startMillis) < T6_MAX_TIMEFRAME) // Record deactivation curve for 5seconds
    {
      if (active && Atx.V12() + Atx.V5() + Atx.V3_3() < ON_OFF_RAIL_SUM_THRESHOLD)
      {
        active = false;
        sessionRecordMark(SESSION_MARK_POFF); // TPOFF: Mark the point where the PSU reached POFF state
      }
      
      updateStatus();
    }
      
    writeSessionOOSFlags(oosflags, maxOosRailData, false);
    
    while (true);
}

void loop_livemode() {
    // Board is in PC Testing Mode
    while (buzzer.isBeeping())
      buzzer.update();

    // Board is in PSU Testing Mode
    Serial.println("LIVE TEST MODE");
    delay(2000); // This value should ensure we are not in a transient mode

    // Sync with PSU status
    /* The PSU should be in one of the following modes
     *  0 = Unknown
     *  1 = T1 - PS_ON (signal triggered, but no response)
     *  2 = T2 - RAMP_UP
     *  3 = T3 - In-Regulation but no PWR_OK
     *  4 = T4 - Ignored
     *  8 = ON
     *  ------------------------ ON/OFF Barrier
     *  5 = T5 - Ignored
     *  6 = T6 - PWR_OK disabled + ramp down
     *  9 = OFF
     *  7 = NOT PRESENT (PSU disconnected or not detected)
     */

    byte psu_mode = 0;
    bool atx_violations = false;

    // For state timeout detection
    unsigned long startMillis = 0;

    // For PON overall OOS stats
    bool oos = false;
    byte oosflags = 0;
    int maxOosRailData[4];
    maxOosRailData[0] = 12.0f;
    maxOosRailData[1] = 5.0f;
    maxOosRailData[2] = 5.0f;
    maxOosRailData[3] = 3.3f;

    while (true) {
      buzzer.update();
      Atx.update();

      // Detect current mode
      if (psu_mode == 0 || psu_mode == 7)
      {
        if (Atx.isPsuPresent()) {
          if (psu_mode != 0) 
          {
            // Was not present and arrived
            Serial.println("PSU arrived.");
            atx_violations = false;
            sessionStart();
          }

          if (Atx.isOn())
          {
            // PSU could be in T1, T2, T3, ON
            if (Atx.isPgGoodPresent()) {
              // PSU is in ON state
              Serial.println("PSU is in ON state");
              psu_mode = 8; // Enter ON state
              sessionRecordMark(SESSION_MARK_ON); // ON: Mark the point where the PSU entered PON
            } else if (Atx.V12() + Atx.V5() + Atx.V3_3() > ON_OFF_RAIL_SUM_THRESHOLD) {
              // PSU is in T2 or T3
              oos = false;
              oos |= IsOutOfSpec(Atx.V5SB(), 5.0f);
              oos |= IsOutOfSpec(Atx.V5(), 5.0f);
              oos |= IsOutOfSpec(Atx.V12(), 12.0f);
              oos |= IsOutOfSpec(Atx.V3_3(), 3.3f);

              if (oos) {
                // PSU is in T2 or out of specs
                psu_mode = 2;
                sessionRecordMark(SESSION_MARK_T2);
                startMillis = millis();
              } else {
                // PSU is in-reg but no PWR_OK. So a T3 mode is assumed
                psu_mode = 3;
                sessionRecordMark(SESSION_MARK_T3); // T3: Mark the point where the PSU entered T3
                startMillis = millis();
              }
            } else {
              // PSU is in T1 state
              psu_mode = 1;
              startMillis = millis(); // Record time as if the PSU was turned on just now
              sessionRecordMark(SESSION_MARK_T1);
            }
          } else {
            // PSU could be in  T6, OFF
            if (Atx.V12() + Atx.V5() + Atx.V3_3() > ON_OFF_RAIL_SUM_THRESHOLD) {
              // PSU is in T6 - ramping down
              psu_mode = 6;
              sessionRecordMark(SESSION_MARK_T6); // T6: Mark the point where the PSU was turned off
              startMillis = millis();
            } else {
              // PSU is in OFF state
              psu_mode = 9;
              Serial.println("PSU is in OFF state");
            }
          }
        } else {
          // No PSU connected.
          if (psu_mode != 7) {
            // Was present before and gone
            Serial.println("PSU disconnected.");
            sessionEnd(!atx_violations);
          }
          psu_mode = 7;
          continue;
        }
      }

      updateStatus(); // Send info over Serial

      // Detect if the PSU has been disconnected
      if (!Atx.isPsuPresent()) {
        psu_mode = 0; // Signal that the PSU has gone missing. It will be processed on the next loop
        continue;
      }

      // Perform diagnosis based on the current psu_mode
      switch (psu_mode)
      {
        case 1: // T1
          if ((millis() - startMillis) > T1_MAX_TIMEFRAME) {
            if (!atx_violations) {
              Serial.println("(!) T1 timeout.");
              buzzer.beep(1200); // 1.2s beep
              SetPsuOk(false);
            }
            atx_violations = true;
          }

          if (IsOutOfSpec(Atx.V5SB(), 5.0f)) {
            if (!atx_violations) {
              Serial.println("(!) V5SB OOS on T1");
              buzzer.beep(1200); // 1.2s beep
              SetPsuOk(false);
            }
            atx_violations = true;
          }

          if (Atx.V12() + Atx.V5() + Atx.V3_3() > ON_OFF_RAIL_SUM_THRESHOLD) {
            psu_mode = 2; // Enter T2
            sessionRecordMark(SESSION_MARK_T2);
            startMillis = millis();
          }
          break;
        case 2: // T2
          if ((millis() - startMillis) > T2_MAX_TIMEFRAME) {
            if (!atx_violations) {
              Serial.println("(!) T2 timeout.");
              buzzer.beep(1200); // 1.2s beep
              SetPsuOk(false);
            }
            atx_violations = true;
          }

          oos = false;
          oos |= IsOutOfSpec(Atx.V5(), 5.0f);
          oos |= IsOutOfSpec(Atx.V12(), 12.0f);
          oos |= IsOutOfSpec(Atx.V3_3(), 3.3f);
          oos |= IsOutOfSpec(Atx.V5SB(), 5.0f);

          if (oos) {
            if (IsOutOfSpec(Atx.V5SB(), 5.0f)) {
              if (!atx_violations) {
                Serial.println("(!) V5SB OOS on T2");
                buzzer.beep(1200); // 1.2s beep
                SetPsuOk(false);
              }
              atx_violations = true;
            }
          } else {
            psu_mode = 3; // Enter T3
            sessionRecordMark(SESSION_MARK_T3); // T3: Mark the point where the PSU entered T3
            startMillis = millis();
          }

          if (Atx.isPgGoodPresent()) {
            // This is a severe violation of the ATX standard
            psu_mode = 8; // Enter ON skipping T3 entirely
            sessionRecordMark(SESSION_MARK_T3); // T3: Mark the point where the PSU entered T3
            sessionRecordMark(SESSION_MARK_ON); // ON: Mark the point where the PSU entered ON
            startMillis = millis();
            Serial.println("(!) SEVERE ATX VIOLATION: PSU SKIPPED T4 ! ! !");
            atx_violations = true;
          }
          break;
        case 3: // T3
          if ((millis() - startMillis) > T3_MAX_TIMEFRAME) {
            if (!atx_violations) {
              Serial.println("(!) T3 timeout.");
              buzzer.beep(1200); // 1.2s beep
              SetPsuOk(false);
            }
            atx_violations = true;
          }

          oos = false;
          oos |= IsOutOfSpec(Atx.V5(), 5.0f);
          oos |= IsOutOfSpec(Atx.V12(), 12.0f);
          oos |= IsOutOfSpec(Atx.V3_3(), 3.3f);
          oos |= IsOutOfSpec(Atx.V5SB(), 5.0f);

          if (oos) {
            if (!atx_violations) {
              Serial.println("(!) RAILS OOS on T3");
              buzzer.beep(1200); // 1.2s beep
              SetPsuOk(false);
            }
            atx_violations = true;
          }

          if (Atx.isPgGoodPresent()) {
            psu_mode = 8; // Enter ON state
            sessionRecordMark(SESSION_MARK_ON); // ON: Mark the point where the PSU entered PON
          }
          break;
        case 6: // T6
          if ((millis() - startMillis) > T6_MAX_TIMEFRAME) {
            if (!atx_violations) {
              Serial.println("(!) T6 timeout.");
              buzzer.beep(1200); // 1.2s beep
              SetPsuOk(false);
            }
            atx_violations = true;
          }

          if (Atx.V12() + Atx.V5() + Atx.V3_3() < ON_OFF_RAIL_SUM_THRESHOLD) {
            psu_mode = 9;
            sessionRecordMark(SESSION_MARK_POFF); // TPOFF: Mark the point where the PSU reached POFF state
          }
          break;
        case 8: // ON
          if (IsOutOfSpec(Atx.V12(), 12.0f))
          {
              if (abs(maxOosRailData[0] - 12.0f) < abs(Atx.V12() - 12.0f)) maxOosRailData[0] = Atx.V12();
              oosflags |= 0b00000001;
          }
          if (IsOutOfSpec(Atx.V5(), 5.0f))
          {
              if (abs(maxOosRailData[1] - 5.0f) < abs(Atx.V5() - 5.0f)) maxOosRailData[1] = Atx.V5();
              oosflags |= 0b00000010;
          }
          if (IsOutOfSpec(Atx.V5SB(), 5.0f))
          {
              if (abs(maxOosRailData[2] - 5.0f) < abs(Atx.V5SB() - 5.0f)) maxOosRailData[2] = Atx.V5SB();
              oosflags |= 0b00000100;
          }
          if (IsOutOfSpec(Atx.V3_3(), 3.3f))
          {
              if (abs(maxOosRailData[3] - 3.3f) < abs(Atx.V3_3() - 3.3f)) maxOosRailData[3] = Atx.V3_3();
              oosflags |= 0b00001000;
          }
          if (!Atx.isPgGoodPresent())
                  oosflags |= 0b00010000;
          
          if (!Atx.isOn() || !Atx.isPgGoodPresent()) {
            psu_mode = 6; // Enter T6
            sessionRecordMark(SESSION_MARK_T6); // T6: Mark the point where the PSU was turned off
            startMillis = millis();
            writeSessionOOSFlags(oosflags, maxOosRailData, true);
          }
          break;
        case 9: // OFF
          if (Atx.isOn()) {
            psu_mode = 1; // Enter T1
            sessionRecordMark(SESSION_MARK_T1);
            startMillis = millis();
          }
          break;
      }
    }
}
