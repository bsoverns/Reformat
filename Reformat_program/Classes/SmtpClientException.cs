using System;
using System.Collections.Generic;
using System.Text;

namespace Reformat_program
{
    public class SmtpClientException : Exception
    {
        private string errorMessage = "";

        public SmtpClientException(string error_message)
        {
            this.errorMessage = error_message;
        }

        public string ErrorMessage
        {
            get
            {
                return this.errorMessage;
            }
        }
    }
}
