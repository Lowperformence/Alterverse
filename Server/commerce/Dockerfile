FROM python:3.12-slim
WORKDIR /app
COPY Server/commerce/requirements.txt /app/requirements.txt
RUN pip install --no-cache-dir -r /app/requirements.txt
COPY Server/commerce /app/Server/commerce
COPY Assets/Alterverse/Resources/Commerce/ShardCatalog.json /app/Assets/Alterverse/Resources/Commerce/ShardCatalog.json
ENV PYTHONPATH=/app/Server/commerce
EXPOSE 8080
CMD ["sh", "-c", "exec gunicorn --bind 0.0.0.0:${PORT:-8080} --workers 2 --threads 2 --timeout 45 wsgi:application"]
