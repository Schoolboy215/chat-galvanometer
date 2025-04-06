const int GALVO_ENABLE = 24;
const int GALVO_OUTPUT = 25;
const int GALVO_MIN = 0;
const int GALVO_NEUTRAL = 128;
const int GALVO_MAX = 252;
const int DEADZONE = 0.05;

int currentPosition;
int wiggle = 10;

void setup() {
  // put your setup code here, to run once:
  pinMode(GALVO_ENABLE, OUTPUT);
  pinMode(GALVO_OUTPUT, OUTPUT);

  pinMode(LED_BUILTIN, OUTPUT);

  enableGalvo();
  setGalvoPosition_int(GALVO_NEUTRAL, true);

  Serial.begin(115200);
}

void loop() {
  if (Serial.available() != 0)
  {
    digitalWrite(LED_BUILTIN, HIGH);
    setGalvoPosition_float(Serial.parseFloat());
    while(Serial. read() >= 0) ;
    digitalWrite(LED_BUILTIN, LOW);
  }
  else
  {
    int setPos = (currentPosition + (random(wiggle) - floor(wiggle/2)));
    setGalvoPosition_int(setPos, false);
    delay(50);
  }
}

void enableGalvo()
{
  digitalWrite(GALVO_ENABLE, HIGH);
}

void disableGalvo()
{
  digitalWrite(GALVO_ENABLE, LOW);  
}

void setGalvoPosition_int(int _pwmValue, bool _overwritePos)
{
  _pwmValue = constrain(_pwmValue, GALVO_MIN, GALVO_MAX);
  analogWrite(GALVO_OUTPUT, _pwmValue);
  if (_overwritePos == true)
  {
    currentPosition = _pwmValue;
  }
}

void setGalvoPosition_float(float _pwmValue)
{
  if (abs(_pwmValue) <= DEADZONE)
  {
    setGalvoPosition_int(GALVO_NEUTRAL, true);
  }
  _pwmValue = floor(_pwmValue * 100);
  setGalvoPosition_int(map(_pwmValue, -100, 100, GALVO_MIN, GALVO_MAX), true);
}
