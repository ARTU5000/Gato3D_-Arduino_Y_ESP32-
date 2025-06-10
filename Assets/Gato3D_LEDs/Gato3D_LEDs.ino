#include <BluetoothSerial.h>

BluetoothSerial SerialBT;

const int led1 = 2;  // Pin para LED del jugador 1
const int led2 = 4;  // Pin para LED del jugador 2
const int led3 = 5;  // Pin para LED del jugador 3

int currentPlayer = 0;

void setup() {
  Serial.begin(115200);
  
  // Configurar pines de los LEDs
  pinMode(led1, OUTPUT);
  pinMode(led2, OUTPUT);
  pinMode(led3, OUTPUT);
  
  // Apagar todos los LEDs inicialmente
  digitalWrite(led1, LOW);
  digitalWrite(led2, LOW);
  digitalWrite(led3, LOW);
  
  // Inicializar Bluetooth
  SerialBT.begin("ESP32_4enLinea"); // Nombre de tu dispositivo Bluetooth
  Serial.println("Bluetooth iniciado. Empareja tu dispositivo!");
}

void loop() {
  if (SerialBT.available()) {
    char receivedChar = SerialBT.read();
    
    // Convertir el carácter recibido a número
    currentPlayer = receivedChar - '0';
    
    // Controlar LEDs según el jugador actual
    switch(currentPlayer) {
      case 1:
        digitalWrite(led1, HIGH);
        digitalWrite(led2, LOW);
        digitalWrite(led3, LOW);
        break;
      case 2:
        digitalWrite(led1, LOW);
        digitalWrite(led2, HIGH);
        digitalWrite(led3, LOW);
        break;
      case 3:
        digitalWrite(led1, LOW);
        digitalWrite(led2, LOW);
        digitalWrite(led3, HIGH);
        break;
      default:
        // Apagar todos si no es válido
        digitalWrite(led1, LOW);
        digitalWrite(led2, LOW);
        digitalWrite(led3, LOW);
    }
    
    Serial.print("Jugador actual: ");
    Serial.println(currentPlayer);
  }
  
  delay(20);
}