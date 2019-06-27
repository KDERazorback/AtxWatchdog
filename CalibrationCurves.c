#ifndef CALIBRATION_CURVES_INCLUDED
#define CALIBRATION_CURVES_INCLUDED

#include <Arduino.h>

float calCurve1(float x1, float o, float x)
{
  if (x < 0.5)
    return 0;

  float y = x1 * x;
  y += o;

  return y;
}

float calCurve2(float x2, float x1, float o, float x)
{
  if (x2 == 0)
    return calCurve1(x1, o, x);

  if (x < 0.5)
    return 0;

  float y = x2 * pow(x, 2);
  y += x1 * x;
  y += o;

  return y;
}

float calCurve3(float x3, float x2, float x1, float o, float x)
{
  if (x3 == 0)
    return calCurve2(x2, x1, o, x);
  
  if (x < 0.5)
    return 0;
  
  float y = x3 * pow(x, 3);
  y += x2 * pow(x, 2);
  y += x1 * x;
  y += o;

  return y;
}

float calCurve4(float x4, float x3, float x2, float x1, float o, float x)
{
  if (x4 == 0)
    return calCurve3(x3, x2, x1, o, x);
  
  if (x < 0.5)
    return 0;
  
  float y = x4 * pow(x, 4);
  y += x3 * pow(x, 3);
  y += x2 * pow(x, 2);
  y += x1 * x;
  y += o;

  return y;
}
#endif
