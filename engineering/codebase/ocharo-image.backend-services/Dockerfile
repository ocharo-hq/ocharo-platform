FROM python:3.12-slim
ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1 NUMBA_CACHE_DIR=/tmp/numba OMP_NUM_THREADS=2
WORKDIR /app
COPY requirements.txt ./
RUN pip install --no-cache-dir -r requirements.txt && useradd --uid 10001 --create-home worker
# The pinned model ships in the image, its checksum checked here, so an environment needs no volume and no download at
# run time. This layer sits above the version stamp, so a worker release reuses it.
COPY model_store.py ./
RUN python model_store.py /models
ARG APP_VERSION
ARG VCS_REF
ENV APP_VERSION=$APP_VERSION VCS_REF=$VCS_REF
COPY worker.py ./
USER worker
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s CMD python -c "import urllib.request; urllib.request.urlopen('http://127.0.0.1:8080/health/live',timeout=2)"
CMD ["uvicorn", "worker:from_environment", "--factory", "--host", "0.0.0.0", "--port", "8080", "--no-access-log", "--limit-concurrency", "8", "--timeout-keep-alive", "5"]
