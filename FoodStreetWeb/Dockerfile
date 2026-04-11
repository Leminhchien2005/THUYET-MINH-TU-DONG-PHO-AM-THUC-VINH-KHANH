FROM libretranslate/libretranslate:latest

# Cloud Run bắt buộc port 8080
ENV PORT=8080

# Chỉ load ngôn ngữ cần thiết (giảm RAM + nhanh hơn)
ENV LT_LOAD_ONLY=en,vi,zh

# Không cần API key
ENV LT_API_KEYS=false

# Expose đúng port Cloud Run
EXPOSE 8080

# Start server
CMD ["libretranslate", "--host", "0.0.0.0", "--port", "8080"]
