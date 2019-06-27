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
#include "Timer.h"

// LEFT SIDE
#define MODE_SEL 4
#define PSU_OK 5
#define TONE 6 // 6=Buzzer, 13=Oboard SMD LED
#define PS_ON_SENSE 7
#define PWR_OK 8
#define PS_ON_TRIGGER 9

#define DATA_PACKET_START 0x11
#define STATUS_PACKET_START 0x12

// RIGHT SIDE
#define V12_SENSE A3
#define V5_SENSE A2
#define V5SB_SENSE A1
#define V3_3_SENSE A0

bool PSU_OK_ACTIVE = false; // ActiveHigh
bool BOARD_PSU_MODE = false; // ActiveHigh

unsigned long lastStatusUpdatePkgSentAt = 0;
unsigned long serialDataPacketsSent = 0;

// Classes
PiezoBuzzer buzzer(TONE);
AtxVoltmeter Atx(V12_SENSE, V5_SENSE, V5SB_SENSE, V3_3_SENSE, PS_ON_SENSE, PS_ON_TRIGGER, PWR_OK);
Timer updatePkgScheduler(&SendStatusUpdatePkg);

// Sketch setup code
void setup() {
  Serial.begin(115200);
  Serial.println("Booting...");

  // Setup Pins
  pinMode(MODE_SEL, INPUT);
  pinMode(PSU_OK, OUTPUT);
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

  // Set ATX Calibration
  Atx.setV12Coefficients  (0.0000000005904564646f, -0.000001065254196f, 0.0007292712878f   , -0.2083356261f     , 25.39229605f        );
  Atx.setV5Coefficients   (0                     , 0                  , 0.3315f            , 0.0330f            , 1.3702f             );
  Atx.setV5sbCoefficients (0                     , 0.000000144284615f , -0.000188086682585f, 0.089558333834226f , -12.314665470356156f);
  Atx.setV3_3Coefficients (0.000000000158821f    , -0.000000279452017f, 0.000186315759286f , -0.050800891265817f, 6.1451224275267f    );

  // Signal BOOT complete
  Serial.println("Boot OK");
  buzzer.beep();
}

//long lastUpdateMicros = 0;
void loop() {
  buzzer.update(); // Ensure the TONE signal is turned off properly

  //unsigned long m = micros();
  
  //if (lastUpdateMicros < m && m < (lastUpdateMicros + 800))
  //  return;

  //lastUpdateMicros = m;
    
  updateStatus(); // Update ATX status
  /*delay(2000);
  Serial.print("ATX.V12: ");
  Serial.println(Atx.V12(), DEC);
  Serial.print("ATX.V5SB: ");
  Serial.println(Atx.V5SB(), DEC);
  Serial.print("ATX.V5: ");
  Serial.println(Atx.V5(), DEC);
  Serial.print("ATX.V3.3: ");
  Serial.println(Atx.V3_3(), DEC);

  Serial.print("ATX.V12Raw: ");
  Serial.println(analogRead(V12_SENSE), DEC);
  Serial.print("ATX.V5SBRaw: ");
  Serial.println(analogRead(V5SB_SENSE), DEC);
  Serial.print("ATX.V5Raw: ");
  Serial.println(analogRead(V5_SENSE), DEC);
  Serial.print("ATX.V3.3Raw: ");
  Serial.println(analogRead(V3_3_SENSE), DEC);*/

  // Update software timers
  updatePkgScheduler.update();

  if (BOARD_PSU_MODE)
  {
    while (buzzer.isBeeping())
      buzzer.update();
    // Board is in PSU Testing Mode
    Serial.println("TEST:START");
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
    
    updateStatus();
    if (Atx.isV5sbPresent() && !Atx.isOn())
      Serial.println("PSU status POFF");
    else
    {
       while (!Atx.isV5sbPresent())
       {
          Serial.print("V5SB is not present. ");
          Serial.print(Atx.V5SB(), DEC);
          Serial.println(" mV");
          delay(2000);
          updateStatus();
       }
       while (!Atx.isPsOnPresent())
       {
          Serial.println("PS_ON is not present.");
          delay(2000);
          updateStatus();
       }
    }

    Serial.println("Turning on...");
    unsigned long endMillis;
    unsigned long startMillis = millis();
    bool v5sbOos = false; // Indicates if V5SB falled below spec during this step
    Atx.turnOn();
    while (true)
    {
      if (millis() - startMillis > 500)
      {
          Serial.println("PSU did not entered T1. Giving up.");
          if (v5sbOos & 0x02 > 0)
            Serial.println("+5VSB disappeared (!).");
          else if (v5sbOos)
            Serial.println("+5VSB falled out of spec (!)."); 

          Serial.print(serialDataPacketsSent, DEC);
          Serial.println(" Data packets sent.");
          while (true);
      }

      updateStatus();

      if (!Atx.isV5sbPresent() || IsOutOfSpec(Atx.V5SB(), 5.0f))
        v5sbOos = (Atx.isV5sbPresent() ? 0x00 : 0x02) | 0x01;

      if (Atx.V12() + Atx.V5() + Atx.V3_3() > 0.5f)
        break; // Enter T1
    }

    endMillis = millis();

    if (v5sbOos)
    {
        if (v5sbOos & 0x02 > 0)
          Serial.println("+5VSB disappeared (!).");
        else if (v5sbOos)
          Serial.println("+5VSB falled out of spec (!).");
        
        Serial.println("Failed at T0");
        delay(1000);
        Atx.turnOff();

        Serial.print(serialDataPacketsSent, DEC);
        Serial.println(" Data packets sent.");
        while (true);    
    }

    unsigned long t0_t = endMillis - startMillis; // T0 time
    startMillis = millis();

    bool v12decayed = false;
    bool v5decayed = false;
    bool v3_3decayed = false;

    float lastV12 = 0.0f;
    float lastV5 = 0.0f;
    float lastV3_3 = 0.0f;

    // TODO: Check if PWR_OK is not asserted before all rails are withing range

    while (true)
    {
      if (millis() - startMillis > 1000)
      {
          Serial.println("PSU did not entered T2. Giving up.");
          if (v5sbOos & 0x02 > 0)
            Serial.println("+5VSB disappeared (!).");
          else if (v5sbOos)
            Serial.println("+5VSB falled out of spec (!)."); 

          Serial.print("T0 lasted for ");
          Serial.print(t0_t, DEC);
          Serial.println("ms");
          delay(1000);
          Atx.turnOff();

          Serial.print(serialDataPacketsSent, DEC);
          Serial.println(" Data packets sent.");
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
        break; // Enter T2
    }

    endMillis = millis();

    if (v5sbOos)
    {
        if (v5sbOos & 0x02 > 0)
          Serial.println("+5VSB disappeared (!).");
        else if (v5sbOos)
          Serial.println("+5VSB falled out of spec (!).");
        
        Serial.println("Failed at T1");
        delay(1000);
        Atx.turnOff();

        Serial.print(serialDataPacketsSent, DEC);
        Serial.println(" Data packets sent.");
        while (true);    
    }

    unsigned long t1_t = endMillis - startMillis; // T1 time
    startMillis = millis();

    while (true)
    {
      if (millis() - startMillis > 5000)
      {
          Serial.println("PSU did not entered PON. Giving up.");
          if (v5sbOos & 0x02 > 0)
            Serial.println("+5VSB disappeared (!).");
          else if (v5sbOos)
            Serial.println("+5VSB falled out of spec (!)."); 

          Serial.print("T0 lasted for ");
          Serial.print(t0_t, DEC);
          Serial.println("ms");

          Serial.print("T1 lasted for ");
          Serial.print(t1_t, DEC);
          Serial.println("ms");

          Serial.print(serialDataPacketsSent, DEC);
          Serial.println(" Data packets sent.");
          while (true);
      }

      updateStatus();

      if (!Atx.isV5sbPresent() || IsOutOfSpec(Atx.V5SB(), 5.0f))
        v5sbOos = (Atx.isV5sbPresent() ? 0x00 : 0x02) | 0x01;

      // TODO: Continue checking main rails for specs

      if (Atx.isPgGoodPresent())
        break;
    }

    endMillis = millis();

    if (v5sbOos)
    {
        if (v5sbOos & 0x02 > 0)
          Serial.println("+5VSB disappeared (!).");
        else if (v5sbOos)
          Serial.println("+5VSB falled out of spec (!).");
        
        Serial.println("Failed at T2");
        delay(1000);
        Atx.turnOff();

        Serial.print(serialDataPacketsSent, DEC);
        Serial.println(" Data packets sent.");
        while (true);    
    }

    unsigned long t2_t = endMillis - startMillis; // T2 time

    // Reached T3
    Serial.println("Boot sequence completed.");
    Serial.print("T0 passed in ");
    Serial.print(t0_t, DEC);
    Serial.println("ms.");
    Serial.print("T1 passed in ");
    Serial.print(t1_t, DEC);
    Serial.println("ms.");
    Serial.print("T2 passed in ");
    Serial.print(t2_t, DEC);
    Serial.println("ms.");

    Serial.println("PON reached in ");
    Serial.print(t0_t + t1_t + t2_t, DEC);
    Serial.println("ms.");

    Serial.print(serialDataPacketsSent, DEC);
    Serial.println(" Data packets sent.");

    delay(3000);
    Atx.turnOff();
    while (true);
  }
  else
  {
    // Board is in PC Testing Mode
    if (Atx.isOn())
    {
      if (IsOutOfSpec(Atx.V12(), 12.0f) || 
        IsOutOfSpec(Atx.V5(), 5.0f) ||
        IsOutOfSpec(Atx.V5SB(), 5.0f) ||
        IsOutOfSpec(Atx.V3_3(), 3.3f) ||
        (!Atx.isPgGoodPresent()))
        {
          // PSU is present, ON and Out of Spec OR PG_GOOD is not present
          buzzer.beep();
          SetPsuOk(false);
          updatePkgScheduler.interval = 1000;
          //updatePkgScheduler.fire();
        }
        else
        {
          // PSU is present, ON and within Spec
          SetPsuOk(true);
          updatePkgScheduler.interval = 2000;
          //updatePkgScheduler.fire();
        }
    }
    else
    {
      // PSU is still not powered on/present 
      if (Atx.isPsuPresent() && IsOutOfSpec(Atx.V5SB(), 5.0f))
      {
        // PSU is present, OFF and V5SB is Out of Spec
        buzzer.beep();
        SetPsuOk(false);
        updatePkgScheduler.interval = 2000;
        //updatePkgScheduler.fire();
      }
      else
      {
        if (Atx.isPsuPresent())
        {
          // PSU is present, OFF and V5SB is within Spec

          if ((Atx.V12() > 0.02f) || 
              (Atx.V5() > 0.02f) ||
              (Atx.V5SB() > 0.02f) ||
              (Atx.V3_3() > 0.02f) ||
              (Atx.isPgGoodPresent()))
              {
                // PSU is present, OFF, V5SB is within Spec but a Primary Rail is still ON! or PG_GOOD is present
                SetPsuOk(false);
                updatePkgScheduler.interval = 1000;
                //updatePkgScheduler.fire();
              }
          else
          {
            // PSU is present, OFF, V5SB is withinc Spec, Primary rails are OFF, and PG_GOOD is OFF too
            SetPsuOk(true);
            updatePkgScheduler.interval = 4000;
            //updatePkgScheduler.fire();
          }
        }
        else
        {
          // PSU is not present
          SetPsuOk(false);
          updatePkgScheduler.interval = 8000;
          //updatePkgScheduler.fire();
        }
      }
    }
  }
}

void sendSerialInt(int value)
{
  byte b = ((value & 0xff00) >> 8);
  Serial.write(b);

  b = (value & 0xff);
  Serial.write(b);
}

void updateStatus()
{
  /*
   * bool PS_ON_SENSE_ACTIVE = false;
   * bool PWR_OK_ACTIVE = false;
   * bool BOARD_PSU_MODE = false;
   */

   bool requireNotify = false;

   bool ps_on_last = Atx.isPsOnPresent();

   bool pwr_ok_last = Atx.isPgGoodPresent();

   Atx.update();
  
   if (ps_on_last != Atx.isPsOnPresent())
      requireNotify = true;

  if (pwr_ok_last != Atx.isPgGoodPresent())
      requireNotify = true;

  if (requireNotify)
    SendStatusUpdatePkg();

   // Send voltages over serial as Ints
   int v12 = Atx.V12() * 1000.0f;
   int v5 = Atx.V5() * 1000.0f;
   int v5sb = Atx.V5SB() * 1000.0f;
   int v3_3 = Atx.V3_3() * 1000.0f;

   Serial.write(DATA_PACKET_START);

   sendSerialInt(v12);
   sendSerialInt(v5);
   sendSerialInt(v5sb);
   sendSerialInt(v3_3);

   serialDataPacketsSent++;
}

void SendStatusUpdatePkg()
{
  Serial.write(STATUS_PACKET_START);

  int status = 0;

  // First byte
  status |= (Atx.isPsOnPresent() & 0x01);
  status = status << 1;
  status |= (Atx.isPgGoodPresent() & 0x01);
  status = status << 1;
  status |= (IsOutOfSpec(Atx.V12(), 12.0f) & 0x01);
  status = status << 1;
  status |= (IsOutOfSpec(Atx.V5(), 5.0f) & 0x01);
  status = status << 1;
  status |= (IsOutOfSpec(Atx.V5SB(), 5.0f) & 0x01);
  status = status << 1;
  status |= (IsOutOfSpec(Atx.V3_3(), 3.3f) & 0x01);
  status = status << 1;
  status |= (PSU_OK_ACTIVE & 0x01);
  status = status << 1;
  status |= (Atx.isTriggered() & 0x01);
  status = status << 1;

  // Second byte
  status |= (BOARD_PSU_MODE & 0x01);
  status = status << 1;
  status |= (buzzer.isBeeping() & 0x01);
  status = status << 6; // Reserved Bits

  sendSerialInt(status);
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
