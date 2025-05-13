CREATE TABLE IF NOT EXISTS "Bookings" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" INTEGER NOT NULL,
    "RouteId" INTEGER NOT NULL,
    "TravelDate" TIMESTAMP NOT NULL,
    "BookingDate" TIMESTAMP NOT NULL,
    "SeatNumber" INTEGER NOT NULL,
    "PaymentAmount" DECIMAL(18,2) NOT NULL,
    "PaymentMethod" TEXT,
    "PaymentStatus" TEXT NOT NULL DEFAULT 'Pending',
    "Status" TEXT NOT NULL DEFAULT 'Pending',
    CONSTRAINT "FK_Bookings_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("id") ON DELETE CASCADE
);

CREATE INDEX "IX_Bookings_UserId" ON "Bookings" ("UserId"); 