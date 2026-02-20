var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS Config (แก้ไขเป็น "AllowAll")
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",  // << 1. เปลี่ยนชื่อ Policy
        policy =>
        {
            policy.AllowAnyOrigin()  // << 2. เปลี่ยนจาก WithOrigins เป็น AllowAnyOrigin
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// (*** 1. เพิ่มบรรทัดนี้ เพื่อให้มันหา index.html ใน wwwroot ***)
app.UseDefaultFiles();
// (*** 2. เพิ่มบรรทัดนี้ เพื่อให้มันเสิร์ฟ CSS/JS/HTML จาก wwwroot ***)
app.UseStaticFiles();

app.UseCors("AllowAll"); // << 3. เรียกใช้ชื่อ Policy ใหม่ให้ตรงกัน

app.MapControllers();

app.Run();
