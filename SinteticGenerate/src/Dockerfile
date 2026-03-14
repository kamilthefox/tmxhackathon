FROM python:3.11-slim

WORKDIR /app

# Копируем requirements
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Копируем код
COPY Config.py NodeManager.py SDclient.py Generate.py ./

# Создаём папку для вывода (будет перекрыта volume)
RUN mkdir -p /app/output

# Точка входа
ENTRYPOINT ["python", "Generate.py"]
CMD []