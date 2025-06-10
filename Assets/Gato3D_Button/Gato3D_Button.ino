const int buttonPin = 2; // Pin donde está conectado el botón

void setup() {
  Serial.begin(9600);
  pinMode(buttonPin, INPUT); // Configura el botón con resistencia pull-up interna
}

void loop() {
  int buttonState = digitalRead(buttonPin);
  
  // Detecta cuando el botón se presiona (cambia de HIGH a LOW)
  if (buttonState == LOW) {
    Serial.println("BUTTON_PRESSED"); // Envía señal a Unity
    delay(50); // Debounce
  }
}