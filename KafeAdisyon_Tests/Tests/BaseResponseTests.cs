using FluentAssertions;
using KafeAdisyon.Common;
using Xunit;

namespace KafeAdisyon.Tests
{
    public class BaseResponseTests
    {
        [Fact(DisplayName = "SuccessResult: Success=true, Data dolu, Message isteğe bağlı")]
        public void SuccessResult_SetsPropertiesCorrectly()
        {
            var response = BaseResponse<string>.SuccessResult("test data", "İşlem tamam");

            response.Success.Should().BeTrue();
            response.Data.Should().Be("test data");
            response.Message.Should().Be("İşlem tamam");
        }

        [Fact(DisplayName = "SuccessResult: Data null olabilir")]
        public void SuccessResult_NullData_Valid()
        {
            var response = BaseResponse<object>.SuccessResult(null);

            response.Success.Should().BeTrue();
            response.Data.Should().BeNull();
        }

        [Fact(DisplayName = "ErrorResult: Success=false, Data null, Message dolu")]
        public void ErrorResult_SetsPropertiesCorrectly()
        {
            var response = BaseResponse<string>.ErrorResult("Bir hata oluştu");

            response.Success.Should().BeFalse();
            response.Data.Should().BeNull();
            response.Message.Should().Be("Bir hata oluştu");
        }

        [Fact(DisplayName = "ErrorResult: Boş mesajla da oluşturulabilir")]
        public void ErrorResult_EmptyMessage_Valid()
        {
            var response = BaseResponse<int>.ErrorResult(string.Empty);

            response.Success.Should().BeFalse();
            response.Message.Should().BeEmpty();
        }
    }
}
